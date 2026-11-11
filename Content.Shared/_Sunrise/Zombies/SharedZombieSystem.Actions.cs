using Content.Shared.Actions;

#pragma warning disable IDE0130 // Namespace не соответствует расположению файла
namespace Content.Shared.Zombies;

public abstract partial class SharedZombieSystem;

public sealed partial class ZombieJumpActionEvent : WorldTargetActionEvent;

public sealed partial class ZombieFlairActionEvent : InstantActionEvent;
