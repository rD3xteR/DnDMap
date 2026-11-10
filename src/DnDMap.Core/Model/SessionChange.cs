using DnDMap.Core.Protocol;

namespace DnDMap.Core.Model;

/// <summary>Что именно изменилось — сервер по этому признаку выбирает SSE-событие.</summary>
public enum SessionChangeKind
{
    Map,
    Tokens,
    View,
    Diameter,
    Viewport
}

/// <summary>Событие изменения сессии. Command = null для изменений, пришедших не из команды.</summary>
public sealed record SessionChange(long Revision, SessionChangeKind Kind, Command? Command);
