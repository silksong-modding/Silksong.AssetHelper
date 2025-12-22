using MonoDetour;
using MonoDetour.HookGen;
using Silksong.AssetHelper.Internal;
using System;
using System.Collections;

namespace Silksong.AssetHelper;

/// <summary>
/// Class for events used by AssetHelper.
/// 
/// Other mods should probably just hook them themselves...
/// </summary>
[MonoDetourTargets(typeof(GameManager))]
[MonoDetourTargets(typeof(QuitToMenu))]
internal static class GameEvents
{
    private static readonly string Id = $"AssetHelper.{nameof(GameEvents)}";

    private static readonly MonoDetourManager mgr = new(Id);

    public static bool IsInGame { get; private set; }

    public static event Action? OnEnterGame;
    public static event Action? OnQuitToMenu;
    public static event Action? OnQuitApplication;

    public static void Hook()
    {
        Md.GameManager.ContinueGame.Postfix(AfterContinueGame, manager: mgr);
        Md.GameManager.StartNewGame.Postfix(AfterStartNewGame, manager: mgr);
        Md.QuitToMenu.Start.Postfix(AfterQuitGame, manager: mgr);
    }

    internal static void AfterQuitApplication()
    {
        foreach (Action a in OnQuitApplication?.GetInvocationList() ?? Array.Empty<Action>())
        {
            ActionUtil.SafeInvoke(a);
        }
    }

    private static void AfterQuitGame(QuitToMenu self, ref IEnumerator returnValue)
    {
        // This method is only called when quitting to menu, even though
        // we're technically breaking the law by hooking the IEnumerator creation method
        AfterGameExit();
    }

    private static void AfterStartNewGame(GameManager self, ref bool permadeathMode, ref bool bossRushMode)
    {
        AfterGameEnter();
    }

    private static void AfterContinueGame(GameManager self)
    {
        AfterGameEnter();
    }

    private static void AfterGameEnter()
    {
        IsInGame = true;

        foreach (Action a in OnEnterGame?.GetInvocationList() ?? Array.Empty<Action>())
        {
            ActionUtil.SafeInvoke(a);
        }
    }

    private static void AfterGameExit()
    {
        IsInGame = false;

        foreach (Action a in OnQuitToMenu?.GetInvocationList() ?? Array.Empty<Action>())
        {
            ActionUtil.SafeInvoke(a);
        }
    }
}
