using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
 
[InitializeOnLoad]
sealed class EnviroDefineSymbol
{
    const string k_Define = "ENVIRO_3";

    static EnviroDefineSymbol()
{
    var targets = Enum.GetValues(typeof(BuildTargetGroup))
        .Cast<BuildTargetGroup>()
        .Where(x => x != BuildTargetGroup.Unknown)
        .Where(x => !IsObsolete(x));

    foreach (var target in targets)
    {
#if UNITY_6000_0_OR_NEWER
        var namedTarget = NamedBuildTarget.FromBuildTargetGroup(target);
        var defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget).Trim();
#else
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target).Trim();
#endif

        var list = defines.Split(new[] { ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (list.Contains(k_Define))
            continue;

        list.Add(k_Define);
        defines = string.Join(";", list);

#if UNITY_6000_0_OR_NEWER
        PlayerSettings.SetScriptingDefineSymbols(namedTarget, defines);
#else
        PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines);
#endif
    }
}

    static bool IsObsolete(BuildTargetGroup group)
    {
        var attrs = typeof(BuildTargetGroup)
            .GetField(group.ToString())
            .GetCustomAttributes(typeof(ObsoleteAttribute), false);

        return attrs != null && attrs.Length > 0;
    } 
}