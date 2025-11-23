using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using REPO_SteamInput.Data;
using Sirenix.Utilities;
using Steamworks;
using Steamworks.Data;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace REPO_SteamInput;

public struct InputStore {
	// public InputStore() { }
	// public InputDictionary<TKey, TValue>
	
	//ActionSets
	internal static InputActionSetHandle_t ActionSetGameControls = SteamInput.Internal.GetActionSetHandle("GameControls");
	internal static InputActionSetHandle_t ActionSetMenuControls = SteamInput.Internal.GetActionSetHandle("MenuControls");
	internal static InputActionSetHandle_t ActionSetSpectatorControls = SteamInput.Internal.GetActionSetHandle("SpectatorControls");
	internal static InputActionSetHandle_t ActionSetItemDistanceLayer = SteamInput.Internal.GetActionSetHandle("ItemDistanceLayer");
	internal static InputActionSetHandle_t ActionSetItemRotateLayer = SteamInput.Internal.GetActionSetHandle("ItemRotateLayer");
	
	//Actions
	internal static ActionHandle ActionInteract = SteamInputHandler.GetDigitalActionHandle("Interact");
	internal static ActionHandle ActionChat = SteamInputHandler.GetDigitalActionHandle("Chat");
	internal static ActionHandle ActionChatDelete = SteamInputHandler.GetDigitalActionHandle("ChatDelete");
	internal static ActionHandle ActionToggleMute = SteamInputHandler.GetDigitalActionHandle("ToggleMute");
	internal static ActionHandle ActionPushToTalk = SteamInputHandler.GetDigitalActionHandle("PushToTalk");
	internal static ActionHandle ActionJump = SteamInputHandler.GetDigitalActionHandle("Jump");
	internal static ActionHandle ActionCrouch = SteamInputHandler.GetDigitalActionHandle("Crouch");
	internal static ActionHandle ActionSprint = SteamInputHandler.GetDigitalActionHandle("Sprint");
	internal static ActionHandle ActionGrab = SteamInputHandler.GetDigitalActionHandle("Grab");
	
	internal static ActionHandle ActionRotate = SteamInputHandler.GetDigitalActionHandle("Rotate");
	internal static ActionHandle ActionPush = SteamInputHandler.GetDigitalActionHandle("Push");
	internal static ActionHandle ActionPull = SteamInputHandler.GetDigitalActionHandle("Pull");
	internal static ActionHandle ActionPushPullLayer = SteamInputHandler.GetDigitalActionHandle("PushPullLayer");
	internal static ActionHandle ActionTumble = SteamInputHandler.GetDigitalActionHandle("Tumble");
	internal static ActionHandle ActionInventory1 = SteamInputHandler.GetDigitalActionHandle("Inventory1");
	internal static ActionHandle ActionInventory2 = SteamInputHandler.GetDigitalActionHandle("Inventory2");
	internal static ActionHandle ActionInventory3 = SteamInputHandler.GetDigitalActionHandle("Inventory3");
	internal static ActionHandle ActionMap = SteamInputHandler.GetDigitalActionHandle("Map");
	internal static ActionHandle ActionMenu = SteamInputHandler.GetDigitalActionHandle("pause_menu");
	
	internal static ActionHandle ExpressionNone = SteamInputHandler.GetDigitalActionHandle("ExpressionNone");
	internal static ActionHandle Expression1 = SteamInputHandler.GetDigitalActionHandle("Expression1");
	internal static ActionHandle Expression2 = SteamInputHandler.GetDigitalActionHandle("Expression2");
	internal static ActionHandle Expression3 = SteamInputHandler.GetDigitalActionHandle("Expression3");
	internal static ActionHandle Expression4 = SteamInputHandler.GetDigitalActionHandle("Expression4");
	internal static ActionHandle Expression5 = SteamInputHandler.GetDigitalActionHandle("Expression5");
	internal static ActionHandle Expression6 = SteamInputHandler.GetDigitalActionHandle("Expression6");
	
	internal static ActionHandle AnalogActionMove = SteamInputHandler.GetAnalogActionHandle("Move"); 
	internal static ActionHandle AnalogActionCamera = SteamInputHandler.GetAnalogActionHandle("Camera"); 
	internal static ActionHandle AnalogActionItemDistance = SteamInputHandler.GetAnalogActionHandle("ItemDistance");
	internal static ActionHandle AnalogActionItemRotate = SteamInputHandler.GetAnalogActionHandle("ItemRotate");
	
	internal static ActionHandle ActionSpectateNext = SteamInputHandler.GetDigitalActionHandle("SpectateNext");
	internal static ActionHandle ActionSpectatePrevious = SteamInputHandler.GetDigitalActionHandle("SpectatePrevious");
	
	internal static ActionHandle AnalogActionZoom = SteamInputHandler.GetAnalogActionHandle("Zoom");
	
	internal static ActionHandle MenuUp = SteamInputHandler.GetDigitalActionHandle("menu_up");
	internal static ActionHandle MenuDown = SteamInputHandler.GetDigitalActionHandle("menu_down");
	internal static ActionHandle MenuLeft = SteamInputHandler.GetDigitalActionHandle("menu_left");
	internal static ActionHandle MenuRight = SteamInputHandler.GetDigitalActionHandle("menu_right");
	internal static ActionHandle MenuSelect = SteamInputHandler.GetDigitalActionHandle("menu_select");
	internal static ActionHandle MenuCancel = SteamInputHandler.GetDigitalActionHandle("menu_cancel");
	internal static ActionHandle AnalogActionMenuScroll = SteamInputHandler.GetAnalogActionHandle("menu_scroll");

	public static TMP_SpriteAsset GlyphSpriteSheet = null!;

	public static Dictionary<InputAction, ActionHandle>? InputActionForSteam;
	
	public static readonly Dictionary<InputKey, ActionHandle> InputNameForSteam = new() {
		{ InputKey.Interact, ActionInteract },
		{ InputKey.Jump, ActionJump },
		{ InputKey.Chat, ActionChat },
		{ InputKey.ChatDelete, ActionChatDelete },
		{ InputKey.ToggleMute, ActionToggleMute },
		{ InputKey.PushToTalk, ActionPushToTalk },
		{ InputKey.Sprint, ActionSprint },
		{ InputKey.Crouch, ActionCrouch },
		{ InputKey.Grab, ActionGrab },
		{ InputKey.Rotate, ActionRotate },
		{ InputKey.Push, ActionPush },
		{ InputKey.Pull, ActionPull },
		{ InputKey.Tumble, ActionTumble },
		{ InputKey.SpectateNext, ActionSpectateNext },
		{ InputKey.SpectatePrevious, ActionSpectatePrevious },
		{ InputKey.Inventory1, ActionInventory1 },
		{ InputKey.Inventory2, ActionInventory2 },
		{ InputKey.Inventory3, ActionInventory3 },
		{ InputKey.Map, ActionMap },
		
		{ InputKey.Menu, ActionMenu },
		{ InputKey.Confirm, MenuSelect },
		{ InputKey.Back, MenuCancel },
		
		{ InputKey.Expression1, Expression1 },
		{ InputKey.Expression2, Expression2 },
		{ InputKey.Expression3, Expression3 },
		{ InputKey.Expression4, Expression4 },
		{ InputKey.Expression5, Expression5 },
		{ InputKey.Expression6, Expression6 },
		
		{ InputKey.Movement, AnalogActionMove },
		{ InputKey.Scroll, AnalogActionItemDistance },
		{ InputKey.MouseInput, AnalogActionCamera },
		{ InputKey.MouseDelta, AnalogActionCamera }
	};
	
	public static readonly Dictionary<InputDigitalActionHandle_t, InputAnalogActionHandle_t> AnalogDigitalActions = new() {
		{ ActionPush.DigitalActionHandle, AnalogActionItemDistance.AnalogActionHandle },
		{ ActionPull.DigitalActionHandle, AnalogActionItemDistance.AnalogActionHandle }
	};


	public static readonly Dictionary<InputDigitalActionHandle_t, ButtonState> InputDown = new() {
		{ ActionInteract.DigitalActionHandle, ButtonState.Normal },
		{ ActionJump.DigitalActionHandle, ButtonState.Normal },
		{ ActionChat.DigitalActionHandle, ButtonState.Normal },
		{ ActionChatDelete.DigitalActionHandle, ButtonState.Normal },
		{ ActionToggleMute.DigitalActionHandle, ButtonState.Normal },
		{ ActionPushToTalk.DigitalActionHandle, ButtonState.Normal },
		{ ActionSprint.DigitalActionHandle, ButtonState.Normal },
		{ ActionCrouch.DigitalActionHandle, ButtonState.Normal },
		{ ActionGrab.DigitalActionHandle, ButtonState.Normal },
		{ ActionRotate.DigitalActionHandle, ButtonState.Normal },
		{ ActionPushPullLayer.DigitalActionHandle, ButtonState.Normal },
		{ ActionPush.DigitalActionHandle, ButtonState.Normal },
		{ ActionPull.DigitalActionHandle, ButtonState.Normal },
		{ ActionTumble.DigitalActionHandle, ButtonState.Normal },
		{ ActionSpectateNext.DigitalActionHandle, ButtonState.Normal },
		{ ActionSpectatePrevious.DigitalActionHandle, ButtonState.Normal },
		{ ActionInventory1.DigitalActionHandle, ButtonState.Normal },
		{ ActionInventory2.DigitalActionHandle, ButtonState.Normal },
		{ ActionInventory3.DigitalActionHandle, ButtonState.Normal },
		{ ActionMap.DigitalActionHandle, ButtonState.Normal },
		{ ActionMenu.DigitalActionHandle, ButtonState.Normal },
		
		{ ExpressionNone.DigitalActionHandle, ButtonState.Normal },
		{ Expression1.DigitalActionHandle, ButtonState.Normal },
		{ Expression2.DigitalActionHandle, ButtonState.Normal },
		{ Expression3.DigitalActionHandle, ButtonState.Normal },
		{ Expression4.DigitalActionHandle, ButtonState.Normal },
		{ Expression5.DigitalActionHandle, ButtonState.Normal },
		{ Expression6.DigitalActionHandle, ButtonState.Normal },
		
		{ MenuUp.DigitalActionHandle, ButtonState.Normal },
		{ MenuDown.DigitalActionHandle, ButtonState.Normal },
		{ MenuLeft.DigitalActionHandle, ButtonState.Normal },
		{ MenuRight.DigitalActionHandle, ButtonState.Normal },
		{ MenuSelect.DigitalActionHandle, ButtonState.Normal },
		{ MenuCancel.DigitalActionHandle, ButtonState.Normal }
	};

	public static Dictionary<ActionHandle, InputActionSetHandle_t[]> InputForActionSet = new() {
		{ ActionInteract, [ActionSetGameControls, ActionSetSpectatorControls] },
		{ ActionJump, [ActionSetGameControls] },
		{ ActionCrouch, [ActionSetGameControls] },
		{ ActionSprint, [ActionSetGameControls] },
		{ ActionGrab, [ActionSetGameControls] },
		{ ActionRotate, [ActionSetGameControls] },
		{ ActionPush, [ActionSetGameControls] },
		{ ActionPull, [ActionSetGameControls] },
		{ ActionPushPullLayer, [ActionSetGameControls] },
		{ ActionTumble, [ActionSetGameControls] },
		{ ActionInventory1, [ActionSetGameControls] },
		{ ActionInventory2, [ActionSetGameControls] },
		{ ActionInventory3, [ActionSetGameControls] },
		{ ActionMap, [ActionSetGameControls] },
		{ ExpressionNone, [ActionSetGameControls] },
		{ Expression1, [ActionSetGameControls] },
		{ Expression2, [ActionSetGameControls] },
		{ Expression3, [ActionSetGameControls] },
		{ Expression4, [ActionSetGameControls] },
		{ Expression5, [ActionSetGameControls] },
		{ Expression6, [ActionSetGameControls] },
		{ ActionMenu, [ActionSetGameControls, ActionSetSpectatorControls] },
		{ ActionChat, [ActionSetGameControls, ActionSetMenuControls, ActionSetSpectatorControls] },
		{ ActionChatDelete, [ActionSetGameControls, ActionSetMenuControls, ActionSetSpectatorControls] },
		{ ActionToggleMute, [ActionSetGameControls, ActionSetSpectatorControls] },
		{ ActionPushToTalk, [ActionSetGameControls, ActionSetSpectatorControls] },
		{ AnalogActionMove, [ActionSetGameControls] },
		{ AnalogActionCamera, [ActionSetGameControls] },
		{ AnalogActionItemDistance, [ActionSetGameControls] },
		{ AnalogActionItemRotate, [ActionSetGameControls] },
		
		{ ActionSpectateNext, [ActionSetSpectatorControls] },
		{ ActionSpectatePrevious, [ActionSetSpectatorControls] },
		{ AnalogActionZoom, [ActionSetSpectatorControls] },
		
		{ MenuUp, [ActionSetMenuControls] },
		{ MenuDown, [ActionSetMenuControls] },
		{ MenuLeft, [ActionSetMenuControls] },
		{ MenuRight, [ActionSetMenuControls] },
		{ MenuSelect, [ActionSetMenuControls] },
		{ MenuCancel, [ActionSetMenuControls] },
		{ AnalogActionMenuScroll, [ActionSetMenuControls] }
	};

	public static Dictionary<InputActionSetHandle_t, (InputActionSetHandle_t, InputDigitalActionHandle_t)[]?> ActionLayersForSets = new() {
		{ ActionSetGameControls, [(ActionSetItemDistanceLayer, ActionPushPullLayer.DigitalActionHandle), (ActionSetItemRotateLayer, ActionRotate.DigitalActionHandle)] }
	};
	
	public static Dictionary<InputDigitalActionHandle_t, (InputActionSetHandle_t, InputDigitalActionHandle_t)> ActionLayersForInput = new() {
		{ ActionPush.DigitalActionHandle, (ActionSetItemDistanceLayer, ActionPushPullLayer.DigitalActionHandle) },
		{ ActionPull.DigitalActionHandle, (ActionSetItemDistanceLayer, ActionPushPullLayer.DigitalActionHandle) }
	};
	public static Dictionary<InputActionSetHandle_t, ActionHandle[]> RelatedInputsForActionLayer = new() {
		{ ActionSetItemDistanceLayer, [AnalogActionItemDistance, ActionPush, ActionPull] }
	};
	
	
	public static readonly ActionHandle[] AllInputs = [
		ActionInteract,
		ActionChat,
		ActionChatDelete,
		ActionToggleMute,
		ActionPushToTalk,
		ActionJump,
		ActionCrouch,
		ActionSprint,
		ActionGrab,
		ActionRotate,
		ActionPush,
		ActionPull,
		ActionPushPullLayer,
		ActionTumble,
		ActionInventory1,
		ActionInventory2,
		ActionInventory3,
		ActionMap,
		ActionMenu,
		ExpressionNone,
		Expression1,
		Expression2,
		Expression3,
		Expression4,
		Expression5,
		Expression6,
		AnalogActionMove,
		AnalogActionCamera,
		AnalogActionItemDistance,
		AnalogActionItemRotate,
		ActionSpectateNext,
		ActionSpectatePrevious,
		AnalogActionZoom,
		MenuUp,
		MenuDown,
		MenuLeft,
		MenuRight,
		MenuSelect,
		MenuCancel,
		AnalogActionMenuScroll
	];

	public static readonly InputDigitalActionHandle_t[] ExpressionInputs = [
		Expression1.DigitalActionHandle,
		Expression2.DigitalActionHandle,
		Expression3.DigitalActionHandle,
		Expression4.DigitalActionHandle,
		Expression5.DigitalActionHandle,
		Expression6.DigitalActionHandle
	];

	public static readonly Dictionary<ActionHandle, string> GlyphInputs = new();
	
	
	internal static void GenerateInputActions(Dictionary<InputKey, InputAction> inputActions) {
		InputActionForSteam = new Dictionary<InputAction, ActionHandle>();

		foreach (var pair in InputNameForSteam) {
			InputActionForSteam.Add(inputActions[pair.Key], pair.Value);
		}
	}

	//This was fun.
	internal static void GenerateAllGlyphs() {
		var originFolder = Path.GetDirectoryName(SteamInput.Internal.GetGlyphForActionOrigin_Legacy(InputActionOrigin.SteamController_A));
		HashSet<string> glyphsStrings = [];
		var buttonNonePath = Path.GetDirectoryName(REPO_SteamInput.Instance.Info.Location) + @"\textures\button_none.png";
		glyphsStrings.Add(buttonNonePath);
		glyphsStrings.AddRange(Directory.GetFiles(originFolder!));
		glyphsStrings.RemoveWhere(s => !s.Contains("_md") || s.Contains("mouse"));
		
		GlyphSpriteSheet = SpriteFactory.BuildFromFiles(glyphsStrings.ToArray());
	}
}