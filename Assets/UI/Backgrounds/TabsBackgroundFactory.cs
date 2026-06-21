public static class TabsBackgroundFactory
{
    public static ITabsBackgroundEffect Create(GuitarBridgeServer owner, bool applyHighwayOverrides = false, bool useMainMenuProfile = false)
    {
        return Create(
            owner,
            applyHighwayOverrides,
            useMainMenuProfile ? GuitarBridgeServer.TabsBackgroundContext.MainMenu : GuitarBridgeServer.TabsBackgroundContext.Gameplay);
    }

    public static ITabsBackgroundEffect Create(
        GuitarBridgeServer owner,
        bool applyHighwayOverrides,
        GuitarBridgeServer.TabsBackgroundContext backgroundContext)
    {
        if (owner == null)
            return null;

        bool useMainMenuProfile = backgroundContext == GuitarBridgeServer.TabsBackgroundContext.MainMenu;
        switch (owner.GetBackgroundModeForContext(backgroundContext))
        {
            case GuitarBridgeServer.TabsBackgroundMode.BlueSky:
                return backgroundContext != GuitarBridgeServer.TabsBackgroundContext.MiniGames && owner.tabSkyUseStageBackdrop
                    ? new TabsStageBackground(applyHighwayOverrides)
                    : new TabsBlueSkyBackground(applyHighwayOverrides, backgroundContext == GuitarBridgeServer.TabsBackgroundContext.MiniGames);
            case GuitarBridgeServer.TabsBackgroundMode.NeonStage:
                return new TabsNeonStageBackground(applyHighwayOverrides, useMainMenuProfile);
            case GuitarBridgeServer.TabsBackgroundMode.SolidColor:
                return null;
            default:
                return new TabsNeonStageBackground(applyHighwayOverrides, useMainMenuProfile);
        }
    }
}
