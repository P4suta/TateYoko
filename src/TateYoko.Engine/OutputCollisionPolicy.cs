namespace TateYoko.Engine;

/// <summary>Controls what happens when the requested output path already exists.</summary>
public enum OutputCollisionPolicy
{
    /// <summary>Chooses the first available numbered name without overwriting an existing file.</summary>
    CreateUnique = 0,

    /// <summary>Atomically replaces the requested file after the new PDF has been validated.</summary>
    ReplaceExisting = 1,
}
