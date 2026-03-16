using Unity.NetCode;

namespace SampleMultiplayer
{
    [UnityEngine.Scripting.Preserve]
    public class NetCodeBootstrap : ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            if (IsBootstrappingEnabledForScene())
            {
                AutoConnectPort = 7979;
                CreateDefaultClientServerWorlds();
                return true;
            }

            AutoConnectPort = 0;
            return false;
        }

        public static bool IsBootstrappingEnabledForScene()
        {
            const NetCodeConfig.AutomaticBootstrapSetting projectDefault =
                NetCodeConfig.AutomaticBootstrapSetting.DisableAutomaticBootstrap;

            var automaticNetcodeBootstrap = DiscoverAutomaticNetcodeBootstrap(logNonErrors: true);
            var automaticBootstrapSettingValue = automaticNetcodeBootstrap
                ? automaticNetcodeBootstrap.ForceAutomaticBootstrapInScene
                : (NetCodeConfig.Global ? NetCodeConfig.Global.EnableClientServerBootstrap : projectDefault);

            return automaticBootstrapSettingValue == NetCodeConfig.AutomaticBootstrapSetting.EnableAutomaticBootstrap;
            
        }
    }
}