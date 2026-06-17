public static class TabsBackgroundFactory
{
    public static ITabsBackgroundEffect Create(GuitarBridgeServer owner, bool applyHighwayOverrides = false, bool useMainMenuProfile = false)
    {
        if (owner == null)
            return null;

        switch (owner.GetBackgroundModeForContext(useMainMenuProfile))
        {
            case GuitarBridgeServer.TabsBackgroundMode.BlueSky:
                return owner.tabSkyUseStageBackdrop
                    ? new TabsStageBackground(applyHighwayOverrides)
                    : new TabsBlueSkyBackground(applyHighwayOverrides);
            case GuitarBridgeServer.TabsBackgroundMode.NeonStage:
                return new TabsNeonStageBackground(applyHighwayOverrides, useMainMenuProfile);
            case GuitarBridgeServer.TabsBackgroundMode.SolidColor:
                return null;
            default:
                return new TabsNeonStageBackground(applyHighwayOverrides, useMainMenuProfile);
        }
    }
}
