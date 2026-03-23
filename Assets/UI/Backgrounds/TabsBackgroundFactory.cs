public static class TabsBackgroundFactory
{
    public static ITabsBackgroundEffect Create(GuitarBridgeServer owner, bool applyHighwayOverrides = false)
    {
        if (owner == null)
            return null;

        switch (owner.tabBackgroundMode)
        {
            case GuitarBridgeServer.TabsBackgroundMode.BlueSky:
                return new TabsBlueSkyBackground(applyHighwayOverrides);
            case GuitarBridgeServer.TabsBackgroundMode.Starfield:
                return new TabsStarfieldBackground(applyHighwayOverrides);
            case GuitarBridgeServer.TabsBackgroundMode.SolidColor:
            default:
                return null;
        }
    }
}
