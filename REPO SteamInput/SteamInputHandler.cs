using System;
using System.Collections.Generic;
using System.IO;
using REPO_SteamInput.Data;
using Steamworks;
using Steamworks.Data;

namespace REPO_SteamInput;

public static class SteamInputHandler {
	// public static SteamInputHandler instence = new SteamInputHandler();

	public static string GetGlyphs(InputActionSetHandle_t actionSetHandle, ActionHandle inputActionHandle) {
		var output = "button_none";
		if (inputActionHandle.IsDigitalAction)
			output = GetDigitalGlyphs(actionSetHandle, inputActionHandle.DigitalActionHandle);
		else if (inputActionHandle.IsAnalogAction)
			output = GetAnalogGlyphs(actionSetHandle, inputActionHandle.AnalogActionHandle);
		return output;
	}
	
	public static string GetDigitalGlyphs(InputActionSetHandle_t actionSetHandle, InputDigitalActionHandle_t digitalActionHandle) {
		var origin = InputActionOrigin.None;
		SteamInput.Internal.GetDigitalActionOrigins(REPO_SteamInput.controller.Handle, actionSetHandle, digitalActionHandle, ref origin);
		return origin == 0 ? "button_none" : Path.GetFileNameWithoutExtension(SteamInput.Internal.GetGlyphForActionOrigin_Legacy(origin));
	}

	public static string GetAnalogGlyphs(InputActionSetHandle_t actionSetHandle, InputAnalogActionHandle_t analogActionHandle) {
		var origin = InputActionOrigin.None;
		SteamInput.Internal.GetAnalogActionOrigins(REPO_SteamInput.controller.Handle, actionSetHandle, analogActionHandle, ref origin);
		return origin == 0 ? "button_none" : Path.GetFileNameWithoutExtension(SteamInput.Internal.GetGlyphForActionOrigin_Legacy(origin));
	}

	public static string GetSteamRootFromPath(string filePath) {
		var dir = new DirectoryInfo(Path.GetDirectoryName(filePath)!);
		while (dir != null) {
			if (string.Equals(dir.Name, "Steam", StringComparison.OrdinalIgnoreCase)) {
				return dir.FullName;
			}
			dir = dir.Parent;
		}
		return "";
	}
	
	private static readonly Dictionary<ActionHandle, string> InputActionHandles = new();
	private static ulong _number = 100;

	public static ActionHandle GetDigitalActionHandle(string actionName) {
		var handle = new ActionHandle(SteamInput.Internal.GetDigitalActionHandle(actionName));
		if (!InputActionHandles.TryAdd(handle, actionName)) {
			REPO_SteamInput.Logger.LogInfo($"GetDigitalActionHandle: {handle.DigitalActionHandle.Value} : {_number}");
			handle = new ActionHandle(new InputDigitalActionHandle_t { Value = _number });
			InputActionHandles.Add(handle, actionName);
			_number++;
		}
		return handle;
	}
	public static ActionHandle GetAnalogActionHandle(string actionName) {
		var handle = new ActionHandle(SteamInput.Internal.GetAnalogActionHandle(actionName));
		if (!InputActionHandles.TryAdd(handle, actionName)) {
			REPO_SteamInput.Logger.LogInfo($"GetAnalogActionHandle: {handle.AnalogActionHandle.Value} : {_number}");
			handle = new ActionHandle(new InputAnalogActionHandle_t { Value = _number });
			_number++;
		}
		return handle;
	}

	// This no longer needed... <Publicize="true"> is a useful tool. Right?
	// Bypass most of Facepunch.Steamworks API limitations... The great majority of features would not be possible otherwise...
	// ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

	// public static bool Init() {
	// 		return (bool) AccessTools.Method(SteamInputInterface.GetType(), "Init").Invoke(SteamInputInterface, null);
	// }
	// public static bool Shutdown() {
	// 	return (bool) AccessTools.Method(SteamInputInterface.GetType(), "Shutdown").Invoke(SteamInputInterface, null);
	// }
	//
	// public static void RunFrame() {
	// 	AccessTools.Method(SteamInputInterface.GetType(), "RunFrame").Invoke(SteamInputInterface, null);
	// }
	//
	// public static int GetConnectedControllers([In] [Out] InputHandle_t[] handlesOut) {
	// 	// var handlesOutValues = new object[handlesOut.Length];
	// 	// for (var i = 0; i < handlesOut.Length; i++) {
	// 	// 	handlesOutValues[i] = handlesOut[i].Value;
	// 	// }
	// 	int num = (int) AccessTools.Method(SteamInputInterface.GetType(), "GetConnectedControllers").Invoke(SteamInputInterface, [handlesOut]);
	// 	// for (var i = 0; i < handlesOut.Length; i++) {
	// 	// 	handlesOut[i].Value = handlesOutValues[i];
	// 	// }
	// 	return num;
	// }
	//
	
	/*public static InputActionSetHandle_t GetActionSetHandle([MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "Steamworks.Utf8StringToNative")] string pszActionSetName) {
		// REPO_SteamInput.Logger.LogInfo(pszActionSetName);
		InputActionSetHandle_t a = new InputActionSetHandle_t {
			Value = AccessTools.Method(SteamInputInterface.GetType(), "GetActionSetHandle").Invoke(SteamInputInterface, [pszActionSetName]),
			Name = pszActionSetName
		};
		return a;
		// REPO_SteamInput.Logger.LogInfo(InputActionSetHandle_t.GetType());
	}

	//
	public static void ActivateActionSet(Controller controllerHandle, InputActionSetHandle_t actionSetHandle) {
		var inputHandle = AccessTools.Field(controllerHandle.GetType(), "Handle").GetValue(controllerHandle);
		AccessTools.Method(SteamInputInterface.GetType(), "ActivateActionSet")
			.Invoke(SteamInputInterface, [inputHandle, actionSetHandle.Value]);
	}

	public static InputActionSetHandle_t GetCurrentActionSet(Controller controllerHandle) {
		var inputHandle = AccessTools.Field(controllerHandle.GetType(), "Handle").GetValue(controllerHandle);
		InputActionSetHandle_t a = new InputActionSetHandle_t {
			Value = AccessTools.Method(SteamInputInterface.GetType(), "GetCurrentActionSet").Invoke(SteamInputInterface, [inputHandle]),
			Name = ""
		};
		// REPO_SteamInput.Logger.LogInfo(InputActionSetHandle_t.ToString());
		return a;
	}

	public static void ActivateActionSetLayer(Controller controllerHandle,
		InputActionSetHandle_t actionSetLayerHandle) {
		var inputHandle = AccessTools.Field(controllerHandle.GetType(), "Handle").GetValue(controllerHandle);
		AccessTools.Method(SteamInputInterface.GetType(), "ActivateActionSetLayer")
			.Invoke(SteamInputInterface, [inputHandle, actionSetLayerHandle.Value]);
	}

	public static void DeactivateActionSetLayer(Controller controllerHandle,
		InputActionSetHandle_t actionSetLayerHandle) {
		var inputHandle = AccessTools.Field(controllerHandle.GetType(), "Handle").GetValue(controllerHandle);
		AccessTools.Method(SteamInputInterface.GetType(), "DeactivateActionSetLayer")
			.Invoke(SteamInputInterface, [inputHandle, actionSetLayerHandle.Value]);
	}

	// public static void DeactivateAllActionSetLayers(InputHandle_t inputHandle) {
	// 	AccessTools.Method(SteamInputInterface.GetType(), "DeactivateAllActionSetLayers").Invoke(SteamInputInterface, [inputHandle]);
	// }
	// public static int GetActiveActionSetLayers(InputHandle_t inputHandle, [In][Out] InputActionSetHandle_t[] handlesOut) {
	// 	return (int) AccessTools.Method(SteamInputInterface.GetType(), "GetActiveActionSetLayers").Invoke(SteamInputInterface, [inputHandle, handlesOut]);
	// }
	public static InputDigitalActionHandle_t GetDigitalActionHandle(
		[MarshalAs(UnmanagedType.CustomMarshaler,
			MarshalType = "Steamworks.Utf8StringToNative")]
		string pszActionName) {
		InputDigitalActionHandle_t a = new InputDigitalActionHandle_t {
			Value = AccessTools.Method(SteamInputInterface.GetType(), "GetDigitalActionHandle")
				.Invoke(SteamInputInterface, [pszActionName]),
			Name = pszActionName
		};
		return a;
	}

	public static DigitalState GetDigitalActionData(Controller controllerHandle,
		InputDigitalActionHandle_t digitalActionHandle) {
		var inputHandle = AccessTools.Field(controllerHandle.GetType(), "Handle").GetValue(controllerHandle);
			var state = (DigitalState)AccessTools.Method(SteamInputInterface.GetType(), "GetDigitalActionData")
			.Invoke(SteamInputInterface, [inputHandle, digitalActionHandle.Value]);
			// REPO_SteamInput.Logger.LogInfo($"digital: {digitalActionHandle.Value} state: {state.}");
			return state;
	}

	public static int GetDigitalActionOrigins(Controller controllerHandle, InputActionSetHandle_t actionSetHandle,
		InputDigitalActionHandle_t digitalActionHandle, ref InputActionOrigin originsOut) {
		var inputHandle = AccessTools.Field(controllerHandle.GetType(), "Handle").GetValue(controllerHandle);
		object[] parms = [inputHandle, actionSetHandle.Value, digitalActionHandle.Value, originsOut.Value];
		int val = (int)AccessTools.Method(SteamInputInterface.GetType(), "GetDigitalActionOrigins")
			.Invoke(SteamInputInterface, parms);
		originsOut.Value = parms[3];
		return val;
	}

	public static InputAnalogActionHandle_t GetAnalogActionHandle(
		[MarshalAs(UnmanagedType.CustomMarshaler,
			MarshalType = "Steamworks.Utf8StringToNative")]
		string pszActionName) {
		InputAnalogActionHandle_t a = new InputAnalogActionHandle_t {
			Value = AccessTools.Method(SteamInputInterface.GetType(), "GetAnalogActionHandle")
				.Invoke(SteamInputInterface, [pszActionName]),
			Name = pszActionName
		};
		return a;
	}

	public static AnalogState GetAnalogActionData(Controller controllerHandle,
		InputAnalogActionHandle_t analogActionHandle) {
		var inputHandle = AccessTools.Field(controllerHandle.GetType(), "Handle").GetValue(controllerHandle);
		return (AnalogState)AccessTools.Method(SteamInputInterface.GetType(), "GetAnalogActionData")
			.Invoke(SteamInputInterface, [inputHandle, analogActionHandle.Value]);
	}

	public static int GetAnalogActionOrigins(Controller controllerHandle, InputActionSetHandle_t actionSetHandle,
		InputAnalogActionHandle_t analogActionHandle, ref InputActionOrigin originsOut) {
		var inputHandle = AccessTools.Field(controllerHandle.GetType(), "Handle").GetValue(controllerHandle);
		object[] parms = [inputHandle, actionSetHandle.Value, analogActionHandle.Value, originsOut.Value];
		int val = (int)AccessTools.Method(SteamInputInterface.GetType(), "GetAnalogActionOrigins")
			.Invoke(SteamInputInterface, parms);
		originsOut.Value = parms[3];
		return val;
	}

	// public static string GetGlyphForActionOrigin(InputActionOrigin eOrigin) {
	// 	
	// 	return (string)AccessTools.Method(SteamInputInterface.GetType(), "GetGlyphForActionOrigin_Legacy")
	// 		.Invoke(SteamInputInterface, [eOrigin.Value]);
	// }

	// public static string GetStringForActionOrigin(InputActionOrigin eOrigin) {
	// 	return (string) AccessTools.Method(SteamInputInterface.GetType(), "GetStringForActionOrigin").Invoke(SteamInputInterface, [eOrigin]);
	// }
	// public static void StopAnalogActionMomentum(InputHandle_t inputHandle, InputAnalogActionHandle_t eAction) {
	// 	AccessTools.Method(SteamInputInterface.GetType(), "StopAnalogActionMomentum").Invoke(SteamInputInterface, [inputHandle, eAction]);
	// }
	// public static MotionState GetMotionData(InputHandle_t inputHandle) {
	// 	return (MotionState) AccessTools.Method(SteamInputInterface.GetType(), "GetMotionData").Invoke(SteamInputInterface, [inputHandle]);
	// }
	public static void TriggerVibration(Controller controllerHandle, ushort usLeftSpeed, ushort usRightSpeed) {
		var inputHandle = AccessTools.Field(controllerHandle.GetType(), "Handle").GetValue(controllerHandle);
		AccessTools.Method(SteamInputInterface.GetType(), "TriggerVibration")
			.Invoke(SteamInputInterface, [inputHandle, usLeftSpeed, usRightSpeed]);
	}

	// public static void SetLEDColor(InputHandle_t inputHandle, byte nColorR, byte nColorG, byte nColorB, uint nFlags) {
	// 	AccessTools.Method(SteamInputInterface.GetType(), "SetLEDColor").Invoke(SteamInputInterface, [inputHandle, nColorR, nColorG, nColorB, nFlags]);
	// }
	public static void TriggerHapticPulse(Controller controllerHandle, SteamControllerPad eTargetPad,
		ushort usDurationMicroSec) {
		var inputHandle = AccessTools.Field(controllerHandle.GetType(), "Handle").GetValue(controllerHandle);
		AccessTools.Method(SteamInputInterface.GetType(), "Legacy_TriggerHapticPulse").Invoke(SteamInputInterface,
			[inputHandle, eTargetPad.Value, usDurationMicroSec]);
	}

	// public static void TriggerRepeatedHapticPulse(InputHandle_t inputHandle, SteamControllerPad eTargetPad, ushort usDurationMicroSec, ushort usOffMicroSec, ushort unRepeat, uint nFlags) {
	// 	AccessTools.Method(SteamInputInterface.GetType(), "TriggerRepeatedHapticPulse").Invoke(SteamInputInterface, [inputHandle, eTargetPad, usDurationMicroSec, usOffMicroSec, unRepeat, nFlags]);
	// }
	public static bool ShowBindingPanel(Controller controllerHandle) {
		var inputHandle = AccessTools.Field(controllerHandle.GetType(), "Handle").GetValue(controllerHandle);
		return (bool)AccessTools.Method(SteamInputInterface.GetType(), "ShowBindingPanel")
			.Invoke(SteamInputInterface, [inputHandle]);
	}*/
	// public static InputType GetInputTypeForHandle(InputHandle_t inputHandle) {
	// 	return (InputType) AccessTools.Method(SteamInputInterface.GetType(), "GetInputTypeForHandle").Invoke(SteamInputInterface, [inputHandle]);
	// }
	// public static InputHandle_t GetControllerForGamepadIndex(int nIndex) {
	// 	return (InputHandle_t) AccessTools.Method(SteamInputInterface.GetType(), "GetControllerForGamepadIndex").Invoke(SteamInputInterface, [nIndex]);
	// }
	// public static int GetGamepadIndexForController(InputHandle_t ulinputHandle) {
	// 	return (int) AccessTools.Method(SteamInputInterface.GetType(), "GetGamepadIndexForController").Invoke(SteamInputInterface, [ulinputHandle]);
	// }
	// public static string GetStringForXboxOrigin(XboxOrigin eOrigin) {
	// 	return (string) AccessTools.Method(SteamInputInterface.GetType(), "GetStringForXboxOrigin").Invoke(SteamInputInterface, [eOrigin]);
	// }
	// public static string GetGlyphForXboxOrigin(XboxOrigin eOrigin) {
	// 	return (string) AccessTools.Method(SteamInputInterface.GetType(), "GetGlyphForXboxOrigin").Invoke(SteamInputInterface, [eOrigin]);
	// }
	// public static InputActionOrigin GetActionOriginFromXboxOrigin(InputHandle_t inputHandle, XboxOrigin eOrigin) {
	// 	return (InputActionOrigin) AccessTools.Method(SteamInputInterface.GetType(), "GetActionOriginFromXboxOrigin").Invoke(SteamInputInterface, [inputHandle, eOrigin]);
	// }
	// public static InputActionOrigin TranslateActionOrigin(InputType eDestinationInputType, InputActionOrigin eSourceOrigin) {
	// 	return (InputActionOrigin) AccessTools.Method(SteamInputInterface.GetType(), "TranslateActionOrigin").Invoke(SteamInputInterface, [eDestinationInputType, eSourceOrigin]);
	// }
	// public static bool GetDeviceBindingRevision(InputHandle_t inputHandle, ref int pMajor, ref int pMinor) {
	// 	return (bool) AccessTools.Method(SteamInputInterface.GetType(), "GetDeviceBindingRevision").Invoke(SteamInputInterface, [inputHandle, pMajor, pMinor]);
	// }
	// public static uint GetRemotePlaySessionID(InputHandle_t inputHandle) {
	// 	return (uint) AccessTools.Method(SteamInputInterface.GetType(), "GetRemotePlaySessionID").Invoke(SteamInputInterface, [inputHandle]);
	// }
}