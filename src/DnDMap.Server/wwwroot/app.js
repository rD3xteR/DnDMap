(() => {
    'use strict';

    const $ = (id) => document.getElementById(id);

    // ?display=1 — экран стола на планшете: только карта, без пульта.
    const displayMode = new URLSearchParams(location.search).has('display');
    if (displayMode) document.body.classList.add('display');

    const els = {
        status: $('status'),
        filesToggle: $('filesToggle'),
        files: $('files'),
        mapSelect: $('mapSelect'),
        openMap: $('openMap'),
        tokenSelect: $('tokenSelect'),
        sizeSelect: $('sizeSelect'),
        addToken: $('addToken'),
        zoom: $('zoom'),
        zoomIn: $('zoomIn'),
        zoomOut: $('zoomOut'),
        zoomValue: $('zoomValue'),
        diameter: $('diameter'),
        diameterValue: $('diameterValue'),
        reset: $('reset'),
        mapImage: $('mapImage'),
        tokens: $('tokens'),
        empty: $('empty'),
        emptyHint: $('emptyHint'),
        emptyAddress: $('emptyAddress'),
        touchControls: $('touchControls'),
        touchZoomIn: $('touchZoomIn'),
        touchZoomOut: $('touchZoomOut'),
        stage: $('stage'),
        note: $('note'),
        mapCount: $('mapCount'),
        tokenCount: $('tokenCount'),
        tabletLock: $('tabletLock'),
        lockBadge: $('lockBadge'),
        sketch: $('sketch'),
        drawToggle: $('drawToggle'),
        drawColor: $('drawColor'),
        drawWidth: $('drawWidth'),
        drawErase: $('drawErase'),
        drawUndo: $('drawUndo'),
        drawClear: $('drawClear'),
        blankCols: $('blankCols'),
        blankRows: $('blankRows'),
        createBlank: $('createBlank')
    };

    const state = {
        session: null,
        layout: null,
        selectedId: null,
        drag: null,
        pan: null,
        sliderActive: false,
        tokenNodes: new Map(),
        manifest: null,
        host: null,
        pointers: new Map(),
        pinch: null,
        sketch: { strokes: [] },
        drawing: false,
        erase: false,
        stroke: null
    };

    const MAX_UPLOAD_BYTES = 256 * 1024 * 1024;
    const IMAGE_EXT = /\.(png|jpe?g)$/i;

    // ---------- сеть ----------

    async function api(path, options) {
        const response = await fetch(path, options);
        if (!response.ok) throw new Error(await errorText(response));
        return response.status === 204 ? null : response.json();
    }

    async function errorText(response) {
        try {
            const body = await response.json();
            if (body && body.error) return body.error;
        } catch {
            // не JSON — покажем статус
        }
        return `HTTP ${response.status}`;
    }

    async function sendCommand(command) {
        try {
            const response = await fetch('/api/commands', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(command)
            });

            if (!response.ok) {
                setStatus(await errorText(response), true);
                return false;
            }

            setStatus('на связи', false, true);
            return true;
        } catch (error) {
            setStatus(`нет связи: ${error.message}`, true);
            return false;
        }
    }

    function setStatus(text, isError = false, isOnline = false) {
        els.status.textContent = text;
        els.status.classList.toggle('error', Boolean(isError));
        els.status.classList.toggle('online', Boolean(isOnline));
    }

    function note(text) {
        els.note.textContent = text || '';
    }

    // ---------- throttling ----------

    function throttle(fn, ms) {
        let last = 0;
        let timer = null;
        let pending = null;

        const invoke = () => {
            last = Date.now();
            const args = pending;
            pending = null;
            if (args) fn(...args);
        };

        const throttled = (...args) => {
            pending = args;
            const now = Date.now();

            if (!timer && now - last >= ms) {
                invoke();
                return;
            }

            if (!timer) {
                timer = setTimeout(() => {
                    timer = null;
                    if (pending) invoke();
                }, Math.max(0, ms - (now - last)));
            }
        };

        throttled.flush = () => {
            if (timer) {
                clearTimeout(timer);
                timer = null;
            }
            if (pending) invoke();
        };

        return throttled;
    }

    const sendMove = throttle((id, x, y) => {
        sendCommand({ type: 'moveToken', tokenId: id, x, y });
    }, 30);

    const sendView = throttle((centerX, centerY, zoom) => {
        sendCommand({ type: 'setView', centerX, centerY, zoom });
    }, 40);

    const sendDiameter = throttle((referencePx) => {
        sendCommand({ type: 'setDiameter', referencePx });
    }, 60);

    // Экран стола сам сообщает CSS-размер сцены: от него зависят fit и visibleRect.
    async function postViewport() {
        const width = els.stage.clientWidth;
        const height = els.stage.clientHeight;

        if (width <= 0 || height <= 0) return;

        try {
            await fetch('/api/viewport', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ width, height })
            });
        } catch {
            // Связь восстановится через SSE; размер дошлём при следующем resize.
        }
    }

    const trackViewport = throttle(() => { postViewport(); }, 250);

    // ---------- состояние и отрисовка ----------

    function applyState(incoming) {
        if (state.drag) {
            const dragged = incoming.tokens.find((token) => token.id === state.drag.id);
            if (dragged) {
                dragged.x = state.drag.x;
                dragged.y = state.drag.y;
            }
        }

        // Пока идёт панорама, серверные кадры отстают — не дёргаем картинку.
        if (state.pan && state.session) {
            incoming.view = state.session.view;
            incoming.visibleRect = state.session.visibleRect;
        }

        state.session = incoming;
        els.tabletLock.checked = Boolean(incoming.tabletInputLocked);
        render();
        syncZoom();
        syncDiameter();
    }

    function render() {
        const session = state.session;
        const stage = els.stage;
        const width = stage.clientWidth;
        const height = stage.clientHeight;

        if (!session || !session.map || width <= 0 || height <= 0) {
            els.mapImage.style.display = 'none';
            els.empty.classList.remove('hidden');
            els.touchControls.classList.add('hidden');
            els.lockBadge.classList.add('hidden');
            els.stage.classList.remove('locked');
            renderEmpty();
            els.tokens.replaceChildren();
            state.tokenNodes.clear();
            state.layout = null;
            renderSketch();
            return;
        }

        const rect = session.visibleRect;
        if (!rect || rect.width <= 0 || rect.height <= 0) return;

        // Блокировка действует только на экране стола: пульт мастера работает всегда.
        const locked = displayMode && Boolean(session.tabletInputLocked);
        els.touchControls.classList.toggle('hidden', locked);
        els.lockBadge.classList.toggle('hidden', !locked);
        els.stage.classList.toggle('locked', locked);

        const scale = Math.min(width / rect.width, height / rect.height);
        const offsetX = (width - rect.width * scale) / 2;
        const offsetY = (height - rect.height * scale) / 2;
        state.layout = { scale, offsetX, offsetY, rect };

        els.empty.classList.add('hidden');

        const image = els.mapImage;
        const source = `/files/map/${encodeURIComponent(session.map.name)}`;
        if (image.dataset.src !== source) {
            image.dataset.src = source;
            image.src = source;
        }

        image.style.display = 'block';
        image.style.left = `${offsetX - rect.x * scale}px`;
        image.style.top = `${offsetY - rect.y * scale}px`;
        image.style.width = `${session.map.width * scale}px`;
        image.style.height = `${session.map.height * scale}px`;

        const alive = new Set();

        for (const token of session.tokens) {
            alive.add(token.id);

            let node = state.tokenNodes.get(token.id);
            if (!node) {
                node = createTokenNode(token.id);
                state.tokenNodes.set(token.id, node);
                els.tokens.appendChild(node);
            }

            const diameter = token.sizeMapPx * scale;
            const x = token.id === state.drag?.id ? state.drag.x : token.x;
            const y = token.id === state.drag?.id ? state.drag.y : token.y;

            node.style.width = `${diameter}px`;
            node.style.height = `${diameter}px`;
            node.style.left = `${offsetX + (x - rect.x) * scale - diameter / 2}px`;
            node.style.top = `${offsetY + (y - rect.y) * scale - diameter / 2}px`;
            node.classList.toggle('selected', token.id === state.selectedId);

            const img = node.firstElementChild;
            const src = `/files/token/${encodeURIComponent(token.file)}`;
            if (img.dataset.src !== src) {
                img.dataset.src = src;
                img.src = src;
            }
        }

        for (const [id, node] of state.tokenNodes) {
            if (alive.has(id)) continue;
            node.remove();
            state.tokenNodes.delete(id);
        }

        renderSketch();
    }

    // ---------- наброски ----------

    const SKETCH_DPR = Math.min(window.devicePixelRatio || 1, 2);

    function renderSketch() {
        const canvas = els.sketch;
        const width = els.stage.clientWidth;
        const height = els.stage.clientHeight;

        const pixelWidth = Math.max(1, Math.round(width * SKETCH_DPR));
        const pixelHeight = Math.max(1, Math.round(height * SKETCH_DPR));

        if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
            canvas.width = pixelWidth;
            canvas.height = pixelHeight;
        }

        const ctx = canvas.getContext('2d');
        if (!ctx) return;

        ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        const layout = state.layout;
        if (!state.session?.map || !layout || width <= 0 || height <= 0) return;

        ctx.setTransform(SKETCH_DPR, 0, 0, SKETCH_DPR, 0, 0);

        for (const stroke of state.sketch.strokes) drawStroke(ctx, stroke, layout);
        if (state.stroke) drawStroke(ctx, state.stroke, layout);
    }

    function drawStroke(ctx, stroke, layout) {
        const points = stroke.points ?? [];
        if (points.length === 0) return;

        const rect = state.session.visibleRect;
        const screenX = (x) => layout.offsetX + (x - rect.x) * layout.scale;
        const screenY = (y) => layout.offsetY + (y - rect.y) * layout.scale;

        // Ластик не рисует: он вырезает уже нарисованное, карта остаётся нетронутой.
        const eraser = stroke.color === 'eraser';

        ctx.save();
        ctx.globalCompositeOperation = eraser ? 'destination-out' : 'source-over';
        ctx.strokeStyle = eraser ? '#000000' : stroke.color;
        ctx.fillStyle = ctx.strokeStyle;
        ctx.lineWidth = Math.max(1, stroke.widthMapPx * layout.scale);
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';

        const first = points[0];

        if (points.length === 1) {
            ctx.beginPath();
            ctx.arc(screenX(first.x), screenY(first.y), ctx.lineWidth / 2, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();
            return;
        }

        ctx.beginPath();
        ctx.moveTo(screenX(first.x), screenY(first.y));

        for (let index = 1; index < points.length; index++) {
            const point = points[index];
            ctx.lineTo(screenX(point.x), screenY(point.y));
        }

        ctx.stroke();
        ctx.restore();
    }

    /// Координата указателя в пикселях карты (с ограничением по её границам).
    function mapPointAt(event) {
        const layout = state.layout;
        const session = state.session;
        if (!layout || !session?.map) return null;

        const bounds = els.stage.getBoundingClientRect();
        const rect = session.visibleRect;

        return {
            x: clampNumber(
                rect.x + (event.clientX - bounds.left - layout.offsetX) / layout.scale,
                0,
                session.map.width),
            y: clampNumber(
                rect.y + (event.clientY - bounds.top - layout.offsetY) / layout.scale,
                0,
                session.map.height)
        };
    }

    function setDrawing(enabled) {
        state.drawing = Boolean(enabled) && !displayMode;

        if (!state.drawing) state.stroke = null;

        els.stage.classList.toggle('drawing', state.drawing);
        els.drawToggle.classList.toggle('active', state.drawing);
        els.drawToggle.textContent = state.drawing ? 'Готово' : 'Рисовать';
        renderSketch();
    }

    function finishStroke() {
        const stroke = state.stroke;
        state.stroke = null;

        if (!stroke) return;

        if (stroke.points.length === 0) {
            renderSketch();
            return;
        }

        const points = stroke.points.map((point) => ({
            x: Math.round(point.x * 10) / 10,
            y: Math.round(point.y * 10) / 10
        }));

        // Показываем штрих сразу, не дожидаясь ответа сервера: иначе он мигнёт.
        state.sketch.strokes.push({
            id: `local-${Date.now()}`,
            color: stroke.color,
            widthMapPx: stroke.widthMapPx,
            points
        });

        renderSketch();

        sendCommand({
            type: 'addSketchStroke',
            color: stroke.color,
            widthMapPx: stroke.widthMapPx,
            points
        });
    }

    // ---------- страница ожидания ----------

    function renderEmpty() {
        if (!displayMode) {
            els.emptyHint.textContent = 'Загрузите карту и нажмите «Открыть карту»';
            els.emptyAddress.classList.add('hidden');
            return;
        }

        const host = state.host;
        const addresses = host?.addresses ?? [];

        els.emptyHint.textContent = addresses.length
            ? 'Пульт мастера — откройте на ноутбуке:'
            : 'Ожидание карты';

        if (!addresses.length) {
            els.emptyAddress.textContent = host
                ? 'Не удалось определить IP-адрес. Подключите планшет к Wi-Fi и перезапустите приложение.'
                : 'Определяем адрес…';
            els.emptyAddress.classList.remove('hidden');
            return;
        }

        const [primary, ...others] = addresses;
        const lines = [document.createElement('div')];
        lines[0].textContent = `http://${primary}:${host.port}/`;

        if (others.length) {
            const alt = document.createElement('small');
            alt.textContent = 'если не открывается, попробуйте: ' +
                others.map((address) => `http://${address}:${host.port}/`).join(' · ');
            lines.push(alt);
        }

        const hint = document.createElement('small');
        hint.textContent = 'затем загрузите карту и нажмите «Открыть карту»';
        lines.push(hint);

        els.emptyAddress.replaceChildren(...lines);
        els.emptyAddress.classList.remove('hidden');
    }

    async function loadHostInfo() {
        try {
            state.host = await api('/api/host');
        } catch (error) {
            note(`адрес хоста: ${error.message}`);
            state.host = { port: location.port || 80, addresses: [] };
        }

        renderEmpty();
    }

    function createTokenNode(id) {
        const node = document.createElement('div');
        node.className = 'token';
        node.dataset.id = id;

        const image = document.createElement('img');
        image.alt = '';
        image.draggable = false;

        node.appendChild(image);
        return node;
    }

    function syncZoom() {
        if (!state.session) return;

        els.zoomValue.textContent = `${state.session.view.zoom.toFixed(2)}×`;

        if (state.sliderActive) return;
        els.zoom.value = String(state.session.view.zoom);
    }

    function syncDiameter() {
        if (!state.session) return;

        const value = Math.round(state.session.baseDiameter.referencePx);
        els.diameterValue.textContent = String(value);

        // Пока пользователь двигает слайдер, не перебиваем его значение ответом сервера.
        if (document.activeElement === els.diameter) return;
        els.diameter.value = String(value);
    }

    // Повторяет ViewGeometry.ClampAxis: кадр не выходит за пределы карты,
    // а карта меньше вьюпорта — центрируется.
    function clampAxis(center, mapLength, visibleLength) {
        if (mapLength <= 0 || visibleLength <= 0) return center;
        if (mapLength <= visibleLength) return mapLength / 2;

        const half = visibleLength / 2;
        return Math.min(Math.max(center, half), mapLength - half);
    }

    function clampNumber(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function optimisticToken(id, x, y) {
        const token = state.session?.tokens.find((item) => item.id === id);
        if (!token) return;
        token.x = x;
        token.y = y;
        render();
    }

    function optimisticView(centerX, centerY, zoom) {
        const session = state.session;
        if (!session || !session.map) return null;

        const rect = session.visibleRect;
        const newWidth = rect.width * session.view.zoom / zoom;
        const newHeight = rect.height * session.view.zoom / zoom;
        const clampedX = clampAxis(centerX, session.map.width, newWidth);
        const clampedY = clampAxis(centerY, session.map.height, newHeight);

        session.view = { centerX: clampedX, centerY: clampedY, zoom };
        session.visibleRect = {
            x: clampedX - newWidth / 2,
            y: clampedY - newHeight / 2,
            width: newWidth,
            height: newHeight
        };

        render();
        syncZoom();
        return session.view;
    }

    function zoomAt(clientX, clientY, zoom) {
        const session = state.session;
        const layout = state.layout;
        if (!session || !session.map || !layout) return;

        const target = Math.min(3, Math.max(1, zoom));
        const rect = session.visibleRect;

        // clientX/Y приходят в координатах окна, а layout — в координатах сцены.
        const bounds = els.stage.getBoundingClientRect();
        const screenX = clientX - bounds.left;
        const screenY = clientY - bounds.top;

        const mapX = rect.x + (screenX - layout.offsetX) / layout.scale;
        const mapY = rect.y + (screenY - layout.offsetY) / layout.scale;
        const relX = (mapX - rect.x) / rect.width;
        const relY = (mapY - rect.y) / rect.height;

        const newWidth = rect.width * session.view.zoom / target;
        const newHeight = rect.height * session.view.zoom / target;

        const centerX = mapX + (0.5 - relX) * newWidth;
        const centerY = mapY + (0.5 - relY) * newHeight;

        const view = optimisticView(centerX, centerY, target);
        if (view) sendView(view.centerX, view.centerY, view.zoom);
    }

    function zoomToCenter(zoom) {
        const session = state.session;
        if (!session || !session.map) return;

        const target = Math.min(3, Math.max(1, zoom));
        const view = optimisticView(session.view.centerX, session.view.centerY, target);
        if (view) sendView(view.centerX, view.centerY, view.zoom);
    }

    // ---------- ввод ----------

    function tokenIdAt(target) {
        const node = target instanceof Element ? target.closest('.token') : null;
        return node ? node.dataset.id : null;
    }

    function selectToken(id) {
        // На экране стола выделение не нужно: токены просто перетаскиваются.
        if (displayMode) return;

        state.selectedId = id;
        render();
    }

    function pointerDistance(first, second) {
        return Math.hypot(first.x - second.x, first.y - second.y);
    }

    function cancelInteraction() {
        if (state.drag) {
            sendMove.flush();
            state.tokenNodes.get(state.drag.id)?.classList.remove('dragging');
            state.drag = null;
        }

        if (state.pan) {
            sendView.flush();
            state.pan = null;
        }
    }

    els.stage.addEventListener('pointerdown', (event) => {
        if (event.button !== 0 || !state.session?.map) return;

        // На планшете с включённым замком не работает ничего: ни панорама, ни зум, ни токены.
        if (displayMode && state.session.tabletInputLocked) {
            event.preventDefault();
            return;
        }

        // Режим рисования забирает указатель целиком — панорама и токены ждут.
        if (state.drawing && !displayMode) {
            const point = mapPointAt(event);
            if (!point) return;

            state.stroke = {
                pointerId: event.pointerId,
                color: state.erase ? 'eraser' : els.drawColor.value,
                widthMapPx: Number(els.drawWidth.value) || 12,
                points: [point]
            };

            els.stage.setPointerCapture(event.pointerId);
            renderSketch();
            event.preventDefault();
            return;
        }

        state.pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });

        // Второй палец включает pinch-зум; начатые drag/pan отменяем.
        if (state.pointers.size === 2) {
            cancelInteraction();
            const [first, second] = [...state.pointers.values()];
            state.pinch = {
                ids: [...state.pointers.keys()],
                lastDistance: pointerDistance(first, second)
            };

            els.stage.setPointerCapture(event.pointerId);
            els.stage.classList.add('busy');
            event.preventDefault();
            return;
        }

        if (state.pointers.size > 2) return;

        const tokenId = tokenIdAt(event.target);

        if (tokenId) {
            const token = state.session.tokens.find((item) => item.id === tokenId);
            if (!token) return;

            selectToken(tokenId);
            state.drag = {
                id: tokenId,
                pointerId: event.pointerId,
                startX: event.clientX,
                startY: event.clientY,
                originX: token.x,
                originY: token.y,
                x: token.x,
                y: token.y
            };

            state.tokenNodes.get(tokenId)?.classList.add('dragging');
        } else {
            selectToken(null);
            state.pan = {
                pointerId: event.pointerId,
                startX: event.clientX,
                startY: event.clientY,
                centerX: state.session.view.centerX,
                centerY: state.session.view.centerY
            };
        }

        els.stage.setPointerCapture(event.pointerId);
        els.stage.classList.add('busy');
        event.preventDefault();
    });

    els.stage.addEventListener('pointermove', (event) => {
        const layout = state.layout;
        if (!layout || !state.session?.map) return;

        if (state.pointers.has(event.pointerId)) {
            state.pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
        }

        // Pinch: масштаб меняется относительно движения пальцев, середина — точка зума.
        if (state.pinch && state.pinch.ids.every((id) => state.pointers.has(id))) {
            const [first, second] = state.pinch.ids.map((id) => state.pointers.get(id));
            const distance = pointerDistance(first, second);

            if (distance > 0 && state.pinch.lastDistance > 0) {
                const factor = distance / state.pinch.lastDistance;
                zoomAt(
                    (first.x + second.x) / 2,
                    (first.y + second.y) / 2,
                    state.session.view.zoom * factor);
            }

            state.pinch.lastDistance = distance;
            event.preventDefault();
            return;
        }

        if (state.stroke && state.stroke.pointerId === event.pointerId) {
            const point = mapPointAt(event);

            if (point) {
                const last = state.stroke.points[state.stroke.points.length - 1];
                const distance = Math.hypot(point.x - last.x, point.y - last.y) * layout.scale;

                // Точки реже 2 px на экране не нужны: они только раздувают штрих.
                if (distance >= 2) {
                    state.stroke.points.push(point);
                    renderSketch();
                }
            }

            event.preventDefault();
            return;
        }

        if (state.drag && state.drag.pointerId === event.pointerId) {
            const dx = (event.clientX - state.drag.startX) / layout.scale;
            const dy = (event.clientY - state.drag.startY) / layout.scale;

            // Центр токена не выходит за карту — как и на сервере.
            state.drag.x = clampNumber(state.drag.originX + dx, 0, state.session.map.width);
            state.drag.y = clampNumber(state.drag.originY + dy, 0, state.session.map.height);

            optimisticToken(state.drag.id, state.drag.x, state.drag.y);
            sendMove(state.drag.id, state.drag.x, state.drag.y);
            event.preventDefault();
            return;
        }

        if (state.pan && state.pan.pointerId === event.pointerId) {
            const dx = (event.clientX - state.pan.startX) / layout.scale;
            const dy = (event.clientY - state.pan.startY) / layout.scale;

            const centerX = state.pan.centerX - dx;
            const centerY = state.pan.centerY - dy;

            const view = optimisticView(centerX, centerY, state.session.view.zoom);
            if (view) sendView(view.centerX, view.centerY, view.zoom);
            event.preventDefault();
        }
    });

    function endPointer(event) {
        state.pointers.delete(event.pointerId);

        if (state.stroke && state.stroke.pointerId === event.pointerId) {
            finishStroke();
        }

        if (state.pinch && state.pinch.ids.includes(event.pointerId)) {
            state.pinch = null;
            sendView.flush();
        }

        if (state.drag && state.drag.pointerId === event.pointerId) {
            sendMove.flush();
            state.tokenNodes.get(state.drag.id)?.classList.remove('dragging');
            state.drag = null;
        }

        if (state.pan && state.pan.pointerId === event.pointerId) {
            sendView.flush();
            state.pan = null;
        }

        els.stage.classList.remove('busy');

        if (els.stage.hasPointerCapture(event.pointerId)) {
            els.stage.releasePointerCapture(event.pointerId);
        }
    }

    els.stage.addEventListener('pointerup', endPointer);
    els.stage.addEventListener('pointercancel', endPointer);

    // Кнопки зума на планшете: не даём им запускать панораму сцены.
    els.touchControls.addEventListener('pointerdown', (event) => event.stopPropagation());
    els.touchControls.addEventListener('click', (event) => {
        const button = event.target instanceof Element ? event.target.closest('button') : null;
        if (!button || !state.session?.map) return;

        const factor = button.id === 'touchZoomIn' ? 1.25 : 1 / 1.25;
        zoomToCenter(state.session.view.zoom * factor);
    });

    els.stage.addEventListener('wheel', (event) => {
        if (displayMode || !state.session?.map) return;
        event.preventDefault();

        const factor = event.deltaY < 0 ? 1.12 : 1 / 1.12;
        zoomAt(event.clientX, event.clientY, state.session.view.zoom * factor);
    }, { passive: false });

    window.addEventListener('keydown', (event) => {
        if (displayMode || event.key !== 'Delete' || !state.selectedId) return;

        sendCommand({ type: 'removeToken', tokenId: state.selectedId });
        state.selectedId = null;
        render();
    });

    const onZoomStart = () => { state.sliderActive = true; };
    const onZoomEnd = () => { state.sliderActive = false; };
    els.zoom.addEventListener('pointerdown', onZoomStart);
    els.zoom.addEventListener('pointerup', onZoomEnd);
    els.zoom.addEventListener('pointercancel', onZoomEnd);
    els.zoom.addEventListener('change', onZoomEnd);
    els.zoom.addEventListener('input', () => zoomToCenter(Number(els.zoom.value)));

    els.zoomIn.addEventListener('click', () => {
        if (state.session) zoomToCenter(state.session.view.zoom * 1.25);
    });

    els.zoomOut.addEventListener('click', () => {
        if (state.session) zoomToCenter(state.session.view.zoom / 1.25);
    });

    els.diameter.addEventListener('input', () => {
        const value = Number(els.diameter.value);
        els.diameterValue.textContent = String(value);
        sendDiameter(value);
    });

    els.diameter.addEventListener('change', () => {
        sendDiameter.flush();
        syncDiameter();
    });

    // ---------- команды панели ----------

    els.openMap.addEventListener('click', async () => {
        const name = els.mapSelect.value;
        if (!name) return;

        // Размеры карты сервер читает из файла сам.
        if (await sendCommand({ type: 'setMap', name, width: 0, height: 0 })) note('');
    });

    els.addToken.addEventListener('click', async () => {
        const file = els.tokenSelect.value;
        if (!file) return;

        if (await sendCommand({ type: 'addToken', file, entitySize: els.sizeSelect.value })) note('');
    });

    els.reset.addEventListener('click', () => sendCommand({ type: 'resetView' }));

    // Замок планшета: состояние живёт в сессии, поэтому его видят оба экрана.
    els.tabletLock.addEventListener('change', () => {
        sendCommand({ type: 'setTabletLock', locked: els.tabletLock.checked });
    });

    els.drawToggle.addEventListener('click', () => setDrawing(!state.drawing));

    els.drawErase.addEventListener('click', () => {
        state.erase = !state.erase;
        els.drawErase.classList.toggle('active', state.erase);

        // Ластик — тоже рисование: включаем режим, чтобы не искать вторую кнопку.
        if (state.erase) setDrawing(true);
    });

    els.drawUndo.addEventListener('click', () => {
        if (state.session?.map) sendCommand({ type: 'undoSketch' });
    });

    els.drawClear.addEventListener('click', () => {
        if (!state.session?.map || state.sketch.strokes.length === 0) return;
        if (!window.confirm('Очистить все наброски на карте?')) return;

        sendCommand({ type: 'clearSketch' });
    });

    // Пустая карта-сетка генерируется на сервере и сразу открывается.
    els.createBlank.addEventListener('click', async () => {
        const columns = clampNumber(Math.round(Number(els.blankCols.value) || 15), 10, 30);
        const rows = clampNumber(Math.round(Number(els.blankRows.value) || 10), 5, 20);

        els.blankCols.value = String(columns);
        els.blankRows.value = String(rows);
        setStatus('создание карты…');

        try {
            const blank = await api('/api/maps/blank', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ columns, rows })
            });

            await refreshManifest();
            els.mapSelect.value = blank.name;
            note(`открыта пустая карта ${blank.columns}×${blank.rows}`);
            setStatus('на связи', false, true);
        } catch (error) {
            note(`пустая карта: ${error.message}`);
            setStatus('ошибка', true);
        }
    });

    els.filesToggle.addEventListener('click', () => els.files.classList.toggle('hidden'));

    // ---------- файлы: загрузка и кэш браузера ----------

    async function putFile(kind, name, blob) {
        const response = await fetch(`/api/files/${kind}/${encodeURIComponent(name)}`, {
            method: 'PUT',
            body: blob
        });

        if (!response.ok) throw new Error(await errorText(response));
        return response.json();
    }

    async function uploadFiles(kind, fileList) {
        const files = Array.from(fileList ?? []);
        if (files.length === 0) return;

        for (const file of files) {
            if (!IMAGE_EXT.test(file.name)) {
                note(`пропущен ${file.name}: только PNG/JPEG`);
                continue;
            }

            if (file.size > MAX_UPLOAD_BYTES) {
                note(`пропущен ${file.name}: больше 256 МБ`);
                continue;
            }

            setStatus(`загрузка ${file.name}…`);

            try {
                await putFile(kind, file.name, file);
                await idbPut({ key: `${kind}:${file.name}`, kind, name: file.name, size: file.size, blob: file });
            } catch (error) {
                note(error.message);
            }
        }

        await refreshManifest();
        setStatus('на связи', false, true);
    }

    document.querySelectorAll('.drop').forEach((zone) => {
        const kind = zone.dataset.kind;
        const input = zone.querySelector('input[type=file]');

        zone.addEventListener('click', () => input.click());
        input.addEventListener('change', () => {
            uploadFiles(kind, input.files);
            input.value = '';
        });

        zone.addEventListener('dragover', (event) => {
            event.preventDefault();
            zone.classList.add('over');
        });

        zone.addEventListener('dragleave', () => zone.classList.remove('over'));

        zone.addEventListener('drop', (event) => {
            event.preventDefault();
            zone.classList.remove('over');
            uploadFiles(kind, event.dataTransfer?.files);
        });
    });

    // ---------- IndexedDB ----------

    const DB_NAME = 'dndmap-files';
    const STORE = 'files';
    let dbPromise = null;

    function openDb() {
        if (dbPromise) return dbPromise;

        dbPromise = new Promise((resolve, reject) => {
            const request = indexedDB.open(DB_NAME, 1);

            request.onupgradeneeded = () => {
                request.result.createObjectStore(STORE, { keyPath: 'key' });
            };
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });

        return dbPromise;
    }

    async function idbPut(record) {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(STORE, 'readwrite');
            tx.objectStore(STORE).put(record);
            tx.oncomplete = () => resolve();
            tx.onerror = () => reject(tx.error);
        });
    }

    async function idbAll() {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(STORE, 'readonly');
            const request = tx.objectStore(STORE).getAll();
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    }

    /// Досылает на хост файлы, которых там нет (после перезапуска сервера/кэша).
    async function syncCache() {
        let records;

        try {
            records = await idbAll();
        } catch {
            return;
        }

        if (!records.length || !state.manifest) return;

        const known = new Map();
        for (const entry of state.manifest.map) known.set(`map:${entry.name}`, entry.size);
        for (const entry of state.manifest.token) known.set(`token:${entry.name}`, entry.size);

        let uploaded = 0;

        for (const record of records) {
            const size = known.get(record.key);
            if (size === record.size) continue;

            try {
                await putFile(record.kind, record.name, record.blob);
                uploaded++;
            } catch (error) {
                note(`не удалось дослать ${record.name}: ${error.message}`);
            }
        }

        if (uploaded > 0) {
            await refreshManifest();
            setStatus(`дослано файлов: ${uploaded}`, false, true);
        }
    }

    // ---------- манифест ----------

    function fillSelect(select, files, emptyText) {
        const previous = select.value;
        select.replaceChildren();

        if (files.length === 0) {
            const option = document.createElement('option');
            option.value = '';
            option.textContent = emptyText;
            select.appendChild(option);
            select.disabled = true;
            return;
        }

        select.disabled = false;

        for (const file of files) {
            const option = document.createElement('option');
            option.value = file.name;
            option.textContent = file.name;
            select.appendChild(option);
        }

        if (files.some((file) => file.name === previous)) {
            select.value = previous;
        }
    }

    async function refreshManifest() {
        try {
            const [maps, tokens] = await Promise.all([
                api('/api/files?kind=map'),
                api('/api/files?kind=token')
            ]);

            state.manifest = { map: maps.files, token: tokens.files };

            fillSelect(els.mapSelect, maps.files, 'нет карт');
            fillSelect(els.tokenSelect, tokens.files, 'нет токенов');

            els.mapCount.textContent = `${maps.files.length} шт.`;
            els.tokenCount.textContent = `${tokens.files.length} шт.`;
        } catch (error) {
            note(`манифест: ${error.message}`);
        }
    }

    // ---------- SSE ----------

    function connect() {
        const source = new EventSource('/api/events');

        source.addEventListener('state', (event) => {
            setStatus('на связи', false, true);
            try {
                applyState(JSON.parse(event.data));
            } catch (error) {
                note(`состояние: ${error.message}`);
            }
        });

        source.addEventListener('sketch', (event) => {
            try {
                const sketch = JSON.parse(event.data);
                state.sketch = { strokes: Array.isArray(sketch?.strokes) ? sketch.strokes : [] };
                renderSketch();
            } catch (error) {
                note(`наброски: ${error.message}`);
            }
        });

        source.onopen = () => setStatus('на связи', false, true);
        source.onerror = () => setStatus('нет связи — переподключение…', true);
    }

    // ---------- старт ----------

    (async function start() {
        if (displayMode) await postViewport();

        try {
            const [session, sketch] = await Promise.all([
                api('/api/session'),
                api('/api/sketch')
            ]);

            state.sketch = { strokes: Array.isArray(sketch?.strokes) ? sketch.strokes : [] };
            applyState(session);
            setStatus('на связи', false, true);
        } catch (error) {
            setStatus(`ошибка: ${error.message}`, true);
        }

        if (displayMode) await loadHostInfo();

        connect();
        await refreshManifest();
        await syncCache();

        if (displayMode) {
            await postViewport();
            window.addEventListener('resize', trackViewport);
            window.addEventListener('orientationchange', trackViewport);

            // Размер сцены может измениться без события window.resize (первый layout, поворот).
            new ResizeObserver(() => trackViewport()).observe(els.stage);
        }
    })();
})();
