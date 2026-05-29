namespace Domains.MediaAggregate;

/// <summary>Top-level media classification, used to pick the processing pipeline.</summary>
public enum MediaKind
{
    Image = 1,
    Video = 2
}