using BepInEx;

namespace Cute_Randimizer;

// TODO - adjust the plugin guid as needed
[BepInAutoPlugin(id: "io.github.ashilstraza.cute_randimizer")]
public partial class Cute_RandimizerPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        // Put your initialization logic here
        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
    }
}
