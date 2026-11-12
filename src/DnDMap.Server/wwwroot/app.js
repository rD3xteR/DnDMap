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
        stage: $('stage'),
        note: $('note'),
        mapCount: $('mapCount'),
        tokenCount: $('tokenCount')
    };

    const state = {
        session: null,
        layout: null,
        selectedId: null,
        drag: null,
        pan: null,
        sliderActive: false,
        tokenNodes: new Map(),
        manifest: null
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
            els.tokens.replaceChildren();
            state.tokenNodes.clear();
            state.layout = null;
            return;
        }

        const rect = session.visibleRect;
        if (!rect || rect.width <= 0 || rect.height <= 0) return;

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
        state.selectedId = id;
        render();
    }

    els.stage.addEventListener('pointerdown', (event) => {
        if (displayMode) return;
        if (event.button !== 0 || !state.session?.map) return;

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

        source.onopen = () => setStatus('на связи', false, true);
        source.onerror = () => setStatus('нет связи — переподключение…', true);
    }

    // ---------- старт ----------

    (async function start() {
        if (displayMode) await postViewport();

        try {
            const session = await api('/api/session');
            applyState(session);
            setStatus('на связи', false, true);
        } catch (error) {
            setStatus(`ошибка: ${error.message}`, true);
        }

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
