public static class TabsBackgroundFactory
{
    public static ITabsBackgroundEffect Create(GuitarBridgeServer owner)
    {
        if (owner == null)
            return null;

        switch (owner.tabBackgroundMode)
        {
            case GuitarBridgeServer.TabsBackgroundMode.BlueSky:
                return new TabsBlueSkyBackground();
            case GuitarBridgeServer.TabsBackgroundMode.Starfield:
                return new TabsStarfieldBackground();
            case GuitarBridgeServer.TabsBackgroundMode.SolidColor:
            default:
                return null;
        }
    }
}
