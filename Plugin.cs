using System;
using System.IO;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.SceneManagement;
using NineSolsAPI;

namespace save_example;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(NineSolsAPICore.PluginGUID)]
[BepInProcess("NineSols.exe")]
public class Plugin : BaseUnityPlugin
{
    internal static Plugin Instance = null!;
    internal new ManualLogSource Logger;
    private Harmony harmony = null!;
    internal CustomFlagManager customFlagManager = null!;

    internal Dictionary<string, FlagsInSpecificScene> allScenes = null!;
    internal GameFlagInt jumpCount = null!;
    internal GameFlagInt killCount = null!;
    private void OnDestroy()
    {
        harmony.UnpatchSelf();
    }
    private void Awake()
    {
        Instance = this;
        Logger = base.Logger;
        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        customFlagManager = new(Instance, "https://github.com/SierraFrey/NineSolsAPI-SaveUtils-ExampleMod/tree/main"); // Constructor requires the instance of the plugin.
        // Optionally, you can include the mod's download URL so that it will be saved in a save file's metadata.
        allScenes = new();

        try
        {
            /*
            Calling these methods allows the API to do most of the work for you by:
            Creating its field
            Setting a default value
            Setting an ID
            Adding it to the game's SaveManager flag dictionary

            Make sure that every name is unique!
            */
            
            jumpCount = customFlagManager.CreateGameFlagInt("jumpCount", 0);
            killCount = customFlagManager.CreateGameFlagInt("killCount", 0);

            /*
            The following 4 flag types can be created with CustomFlagManager:
            ScriptableDataBool - Bool
            GameFlagInt - Int
            ScriptableDataFloat - Float
            GameFlagString - String

            If you want to save something more complicated, you can create classes that use the CustomFlagManager methods in its constructor.
            */
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string name = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                allScenes.Add(name, new FlagsInSpecificScene(name));
            }
            harmony.PatchAll();
        }
        catch (Exception e)
        {
            Logger.LogError($"Plugin {MyPluginInfo.PLUGIN_GUID} failed to load! {e}");
        }
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }
    
    public class FlagsInSpecificScene
    {
        private static CustomFlagManager customFlagManager => Plugin.Instance.customFlagManager;
        public FlagsInSpecificScene(string name)
        {
            breakablesDestroyed = customFlagManager.CreateGameFlagInt($"{name}.breakablesDestroyed", 0);
            timesEntered = customFlagManager.CreateGameFlagInt($"{name}.timesEntered", 0);
        }
        public GameFlagInt breakablesDestroyed;
        public GameFlagInt timesEntered;
    }
}

[HarmonyPatch]
public class Patches
{
    private static Plugin Instance => Plugin.Instance;

    [HarmonyPatch(typeof(Player), nameof(Player.MechanicalJump))]
    [HarmonyPostfix]
    private static void MechanicalJump()
    {
        Instance.jumpCount.CurrentValue++;
        ToastManager.Toast($"Jumps: {Instance.jumpCount.CurrentValue}");
    }

    [HarmonyPatch(typeof(MonsterBase), nameof(MonsterBase.Die))]
    [HarmonyPostfix]
    private static void Die()
    {
        Instance.killCount.CurrentValue++;
        ToastManager.Toast($"Kills: {Instance.killCount.CurrentValue}");
    }

    [HarmonyPatch(typeof(DebrisBreakable), nameof(DebrisBreakable.Break))]
    [HarmonyPostfix]
    private static void Break()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        GameFlagInt flag = Instance.allScenes[sceneName].breakablesDestroyed;
        flag.CurrentValue++;
        ToastManager.Toast($"Breakables destroyed in this scene: {flag.CurrentValue}");
    }

    [HarmonyPatch(typeof(GameLevel), nameof(GameLevel.Awake))]
    [HarmonyPostfix]
    private static void Awake(GameLevel __instance)
    {
        if (!Instance.allScenes.ContainsKey(__instance.SceneName))
            return;
        GameFlagInt flag = Instance.allScenes[__instance.SceneName].timesEntered;
        flag.CurrentValue++;
        ToastManager.Toast($"Times you entered this scene ({__instance.SceneName}): {flag.CurrentValue}");
    }
}


