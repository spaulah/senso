namespace Senso.Mcp.Input;

public enum MediaKind { Image, Audio }

/// <summary>Media that has been loaded and validated (extension/signature, size).</summary>
public sealed record ResolvedMedia(byte[] Bytes, string Format, string MimeType, string FileName);
