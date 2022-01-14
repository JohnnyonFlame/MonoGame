// Compile with mcs, referencing the necessary assemblies e.g.:
// mcs -target:library -reference:'StardewValley.exe' -reference:'MonoGame.Framework.dll' -reference:'0Harmony.dll' -optimize+ patches/StardewPatches.cs

using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley;

[ModEntryPoint]
public static class StardewPatches
{
	public static void Main()
	{
		Console.Out.WriteLine("Found StardewPatches, running...");
		new Harmony("com.github.johnnyonflame.StardewPatches").PatchAll(Assembly.GetExecutingAssembly());
	}

	[HarmonyPatch(typeof(Options))]
	[HarmonyPatch("uiScale", MethodType.Getter)]
	private class Options__get_desiredUIScale
	{
		// Fit UI to the screen.
		private static void Postfix(ref float __result)
		{
			float hratio = (float)Game1.game1.Window.ClientBounds.Height / 768f;
			float wratio = (float)Game1.game1.Window.ClientBounds.Width / 1366f;
			float ratio = (hratio < wratio) ? hratio : wratio;
			__result *= ratio;
		}
	}

	[HarmonyPatch(typeof(InputState))]
	[HarmonyPatch("SetMousePosition")]
	private class InputState__SetMousePosition
	{
		private static bool Prefix(int x, int y, ref InputState __instance)
		{
			// Mouse input is completely broken, we'll disable it here.
			if (!Game1.game1.IsMainInstance)
			{
				Traverse.Create(__instance).Field("_simulatedMousePosition").SetValue(new Point(x, y));
				return false;
			}
			Traverse traverse = Traverse.Create(__instance).Field("_currentMouseState");
			MouseState mouseState = (MouseState)traverse.GetValue();
			traverse.SetValue(new MouseState(x, y, mouseState.ScrollWheelValue, mouseState.LeftButton, mouseState.MiddleButton, mouseState.RightButton, mouseState.XButton1, mouseState.XButton2));
			return false;
		}
	}

	[HarmonyPatch(typeof(InputState))]
	[HarmonyPatch("UpdateStates")]
	private class InputState__UpdateStates
	{
		private static bool Prefix(ref InputState __instance)
		{
			// Mouse input is completely broken, we'll disable it here too
			Traverse traverse4 = Traverse.Create(__instance).Field("_currentKeyboardState");
			Traverse traverse2 = Traverse.Create(__instance).Field("_currentMouseState");
			Traverse traverse3 = Traverse.Create(__instance).Field("_currentGamepadState");
			traverse4.SetValue(Keyboard.GetState());
			if (Game1.playerOneIndex >= PlayerIndex.One)
			{
				traverse3.SetValue(GamePad.GetState(Game1.playerOneIndex));
				return false;
			}
			if (traverse2.GetValue() == null)
			{
				traverse2.SetValue(default(MouseState));
			}
			traverse3.SetValue(default(GamePadState));
			return false;
		}
	}

	[HarmonyPatch(typeof(Options))]
	[HarmonyPatch("zoomLevel", MethodType.Getter)]
	private class Options__zoomLevel_getter
	{
		// TODO:: Breaks on hidpi screens like rg552 - fix it.
		private static bool Prefix(ref float __result, ref Options __instance)
		{
			if (Game1.game1.takingMapScreenshot)
			{
				__result = __instance.baseZoomLevel * 0.5f;
				return false;
			}
			__result = __instance.baseZoomLevel * 0.5f * Game1.game1.zoomModifier;
			return false;
		}
	}
}