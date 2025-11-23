using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using REPO_SteamInput.Data;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;
using Color = UnityEngine.Color;

// ReSharper disable HeuristicUnreachableCode
#pragma warning disable CS0162 // Unreachable code detected

namespace REPO_SteamInput;

[BepInPlugin("slenered.REPO_SteamInput", "REPO SteamInput", BuildInfo.Version)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class REPO_SteamInput : BaseUnityPlugin {
	internal static REPO_SteamInput Instance { get; private set; } = null!;
	internal new static ManualLogSource Logger => Instance._logger;
	private ManualLogSource _logger => base.Logger;
	internal Harmony? Harmony { get; set; }
	private const bool DEBUG = false;

	private static readonly Thread GlyphCheck = new(CheckForGlyphChange);
	private static TMP_SpriteAsset TutorialOriginalEmojis = null!;
	private static TMP_SpriteAsset ItemInfoOriginalEmojis = null!;
	private static TMP_SpriteAsset OriginalEmojis = null!;
	
	public static bool ActionSetItemDistanceLayerActive;
	public static bool ActionSetItemRotateLayerActive;
	public static bool ShowedKeyboard;
	
	internal static Controller controller;
	// internal static InputActionSetHandle_t controllerLastActionSet;
	internal static bool controllerActive;
	internal static float controllerRumbleTime;
	// internal static float controllerRumble;
	internal static float clickCooldown;
	
	internal static Dictionary<MenuPage, MenuSelectableElement> ActiveButtons = new();
	internal static bool openedOnTop;
	
	internal static AnalogState stickCameraState;
	internal static AnalogState stickRotateState;
	
	public static ConfigEntry<bool> GrabToggle = null!;
	private void Awake() {
		GlyphCheck.IsBackground = true;
		Instance = this;
		gameObject.transform.parent = null;
		gameObject.hideFlags = HideFlags.HideAndDontSave;
		
		GrabToggle = Config.Bind("Controls", "Grab Toggle", false, new ConfigDescription("Lets the game handle grab toggles instead of SteamInput. (No more randomly grabbing something.)"));
		
		Patch();
		Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
	}

	private void Update() {
		// controller = 
		bool foundController = false;
		foreach (var c in SteamInput.Controllers) {
			if (c.InputType == InputType.Unknown) continue;
			controller = c;
			foundController = true;
			break;
		}

		if (!foundController) {
			controllerActive = false;
			return;
		}
		
		if (!controllerActive) {
			SteamInput.Internal.TriggerVibration(controller.Handle, 0, 0);
			controllerRumbleTime = 0f;
			if (ControllerAnyInput()) {
				GrabToggle.ConfigFile.Reload();
				controllerActive = true;
				// if (ActiveMenu == MenuPageIndex.SettingsControls)
				// 	MenuManager.instance.PageCloseAllAddedOnTop();
				controllerRumbleTime = 0.25f;
				SteamInput.Internal.TriggerVibration(controller.Handle, ushort.MaxValue/6, ushort.MaxValue/6);
				SteamInput.Internal.ActivateActionSet(controller.Handle, MenuManager.instance.currentMenuState == 0 ? InputStore.ActionSetMenuControls : PlayerAvatar.instance.spectating && SpectateCamera.instance.currentState != SpectateCamera.State.Head ? InputStore.ActionSetSpectatorControls : InputStore.ActionSetGameControls);
				// print($"currentMenuState: {MenuManager.instance.currentMenuState} Spectating: {PlayerAvatar.instance.spectating && SpectateCamera.instance.currentState != SpectateCamera.State.Head} : {SteamInput.Internal.GetCurrentActionSet(controller.Handle)}");
				
				UpdatePrompts();
				print("controller active");
			}
		} else {
			ControllerUpdate();
			var keyboardUsed = !string.IsNullOrEmpty(Input.inputString) || Input.anyKeyDown;
			var mouseUsed = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2) || Input.mouseScrollDelta.sqrMagnitude > 0 || Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0;
			if (keyboardUsed || mouseUsed) {
				controllerActive = false;
				SteamInput.Internal.TriggerVibration(controller.Handle, 0, 0);
				controllerRumbleTime = 0f;
				
				UpdatePrompts();
				print("controller inactive");
			}
		}
	}

	internal static void UpdatePrompts() {
		if (TutorialDirector.instance && TutorialDirector.instance.tutorialActive) {
			string text1 = TutorialDirector.instance.tutorialPages[TutorialDirector.instance.currentPage].text;
			string dummyText = TutorialDirector.instance.tutorialPages[TutorialDirector.instance.currentPage].dummyText;
			string dummyTextString = InputManager.instance.InputDisplayReplaceTags(dummyText);
			VideoClip video = TutorialDirector.instance.tutorialPages[TutorialDirector.instance.currentPage].video;
			string text2 = InputManager.instance.InputDisplayReplaceTags(text1);
			TutorialUI.instance.SetPage(video, text2, dummyTextString, false);
			// if (TutorialDirector.instance.currentPage == 0)
			// else
			// 	TutorialUI.instance.SetPage(video, text2, dummyTextString);
			TutorialDirector.instance.arrowDelay = 4f;
		}
		if (MenuManager.instance.currentMenuPage && MenuManager.instance.currentMenuPage.menuPageIndex == MenuPageIndex.Lobby) {
			MenuManager.instance.currentMenuPage.GetComponent<MenuPageLobby>().UpdateChatPrompt();
		}
		if (SpectateHeadUI.instance)
			SpectateHeadUI.instance.promptHidden = true;
	}

	internal static void DebugPrint(string msg) {
		if (DEBUG) print(msg);
	}

	internal static bool ControllerAnyInput(bool ignoreAnalog = false) {
		return InputStore.AllInputs.Select(input => {
				if (input.IsDigitalAction) {
					return SteamInput.Internal.GetDigitalActionData(controller.Handle, input.DigitalActionHandle).Pressed;
				}
				if (!input.IsAnalogAction || ignoreAnalog) return false;
				var stick = SteamInput.Internal.GetAnalogActionData(controller.Handle, input.AnalogActionHandle);
				var xy = Mathf.Abs(stick.X) + Mathf.Abs(stick.Y);
				return xy != 0;
			}
		).Any(pressed => pressed);
	}

	internal static void ControllerUpdate() {
		// SteamInput.RunFrame();
		stickCameraState = SteamInput.Internal.GetAnalogActionData(controller.Handle, InputStore.AnalogActionCamera.AnalogActionHandle);
		stickRotateState = SteamInput.Internal.GetAnalogActionData(controller.Handle, InputStore.AnalogActionItemRotate.AnalogActionHandle);
		
		if (controllerRumbleTime > 0f) {
			controllerRumbleTime -= Time.deltaTime;
		}

		if (controllerRumbleTime <= 0) {
			controllerRumbleTime = 0f;
			SteamInput.Internal.TriggerVibration(controller.Handle, 0, 0);
		}
		
		foreach (var input in InputStore.AllInputs.Where(i => i.IsDigitalAction)) {
			if (InputStore.ExpressionInputs.Contains(input.DigitalActionHandle)) {
				var pressed = SteamInput.Internal.GetDigitalActionData(controller.Handle, input.DigitalActionHandle).Pressed;
				var state = InputStore.InputDown[input.DigitalActionHandle];
				if (pressed) {
					switch (state) { // N > H >> H > P > R >> R > N
						case ButtonState.Normal:
							InputStore.InputDown[input.DigitalActionHandle] =  ButtonState.Held;
							// print($"input {input} held");
						break;
						case ButtonState.Pressed:
							InputStore.InputDown[input.DigitalActionHandle] =  ButtonState.Released;
						break;
						case ButtonState.Released:
						case ButtonState.Held:
						default:
						break;
					}	
				} else {
					switch (state) {
						case ButtonState.Released:
							InputStore.InputDown[input.DigitalActionHandle] =  ButtonState.Normal;
							break;
						case ButtonState.Held:
							InputStore.InputDown[input.DigitalActionHandle] =  ButtonState.Pressed;
							break;
						case ButtonState.Pressed:
						case ButtonState.Normal:
						default:
							break;
					}	
				}
				
				continue;
			}
			if (SteamInput.Internal.GetDigitalActionData(controller.Handle, input.DigitalActionHandle).Pressed) {
				switch (InputStore.InputDown[input.DigitalActionHandle]) {
					case ButtonState.Normal:
					case ButtonState.Released:
						InputStore.InputDown[input.DigitalActionHandle] =  ButtonState.Pressed;
						break;
					case ButtonState.Pressed:
						InputStore.InputDown[input.DigitalActionHandle] =  ButtonState.Held;
						break;
					case ButtonState.Held:
					default:
						break;
				}	
			} else {
				switch (InputStore.InputDown[input.DigitalActionHandle]) {
                    case ButtonState.Pressed:
                    case ButtonState.Held:
                    	InputStore.InputDown[input.DigitalActionHandle] =  ButtonState.Released;
                    	break;
                    case ButtonState.Released:
                    	InputStore.InputDown[input.DigitalActionHandle] =  ButtonState.Normal;
                    	break;
                    case ButtonState.Normal:
                    default:
	                    break;
				}	
			}
		}

		if (InputStore.InputDown[InputStore.ExpressionNone.DigitalActionHandle] > ButtonState.Released) {
			foreach (var input in InputStore.ExpressionInputs) {
				InputStore.InputDown[input] = ButtonState.Normal;
			}
		}
		
		switch (InputStore.InputDown[InputStore.ActionRotate.DigitalActionHandle]) {
			case > ButtonState.Released when !ActionSetItemRotateLayerActive:
				ActionSetItemRotateLayerActive =  true;
				SteamInput.Internal.ActivateActionSetLayer(controller.Handle, InputStore.ActionSetItemRotateLayer);
			break;
			case <= ButtonState.Released when ActionSetItemRotateLayerActive:
				ActionSetItemRotateLayerActive =  false;
				SteamInput.Internal.DeactivateActionSetLayer(controller.Handle, InputStore.ActionSetItemRotateLayer);
			break;
		} 
		
		switch (InputStore.InputDown[InputStore.ActionPushPullLayer.DigitalActionHandle]) {
			case > ButtonState.Released when !ActionSetItemDistanceLayerActive:
				ActionSetItemDistanceLayerActive =  true;
				SteamInput.Internal.ActivateActionSetLayer(controller.Handle, InputStore.ActionSetItemDistanceLayer);
			break;
			case <= ButtonState.Released when ActionSetItemDistanceLayerActive:
				ActionSetItemDistanceLayerActive =  false;
				SteamInput.Internal.DeactivateActionSetLayer(controller.Handle, InputStore.ActionSetItemDistanceLayer);
			break;
		}
		// print($"ItemDistanceLayer: {ActionSetItemDistanceLayerActive}, ItemRotateLayer: {ActionSetItemRotateLayerActive}");

		if (GlyphCheck.ThreadState == ThreadState.Stopped)
			GlyphCheck.Start();
	}

	private static void CheckForGlyphChange() {
		var foundChange = false;
		foreach (var inputSet in InputStore.InputForActionSet) {
			foreach (var actionSet in inputSet.Value) {
				var glyph = SteamInputHandler.GetGlyphs(actionSet, inputSet.Key);
				if (InputStore.GlyphInputs.TryGetValue(inputSet.Key, out var glyphInput)) {
					if (glyphInput == glyph) continue;
					foundChange = true;
					InputStore.GlyphInputs[inputSet.Key] = glyph;
				} else {
					InputStore.GlyphInputs.Add(inputSet.Key, glyph);
				}
			}
		}

		if (foundChange) {
			UpdatePrompts();
		}
	}

	internal static float PushAndPull(float magnitude) {
		return 0.2f * Mathf.Abs(controllerActive ? magnitude : 1f);
	}

	internal void Patch() {
		Harmony ??= new Harmony(Info.Metadata.GUID);
		
		Harmony.PatchAll(typeof(InputPatches));
		Harmony.PatchAll(typeof(MenuPatches));
	}
	
	internal void Unpatch() {
		Harmony?.UnpatchSelf();
	}

	private static class MenuPatches {
		[HarmonyPatch(typeof(MenuButton), nameof(MenuButton.HoverLogic))]
		[HarmonyTranspiler]
		internal static IEnumerable<CodeInstruction> MenuButton_HoverLogic(IEnumerable<CodeInstruction> instructions) {
			return new CodeMatcher(instructions)
				.MatchForward(false, new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Input), nameof(Input.GetMouseButtonDown))))
				.Advance(-1)
				.SetOpcodeAndAdvance(OpCodes.Nop)
				.SetOperandAndAdvance(AccessTools.Method(typeof(MenuPatches), nameof(GetMouseOrButtonDownZero)))
				.InstructionEnumeration();
		}
		
		internal static bool GetMouseOrButtonDownZero() {
			return Input.GetMouseButtonDown(0) || SemiFunc.InputDown(InputKey.Confirm);
		}
		
		[HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.UIMouseHover))]
		[HarmonyPrefix]
		internal static bool UIMouseHover(MenuPage parentPage, RectTransform rectTransform, string menuID, float xPadding, float yPadding, ref bool __result) {
			// print($"Menu: {parentPage.menuHeaderName}, {parentPage.currentPageState}, {parentPage.addedPageOnTop}");
			if (!controllerActive) return true;
			__result = (parentPage.currentPageState == MenuPage.PageState.Active || parentPage.addedPageOnTop && parentPage.parentPage.currentPageState == MenuPage.PageState.Active) && ActiveButtons.TryGetValue(!parentPage.addedPageOnTop ? parentPage : parentPage.parentPage, out var ActiveButton) && ActiveButton!.menuID == menuID;
			return false;
		}
		
		
		[HarmonyPatch(typeof(MenuElementHover), nameof(MenuElementHover.Update))]
		[HarmonyTranspiler]
		internal static IEnumerable<CodeInstruction> MenuElementHover_Update(IEnumerable<CodeInstruction> instructions) {
			var code = new CodeMatcher(instructions)
				.MatchForward(false, new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(SemiFunc), nameof(SemiFunc.UIMouseHover))))
				.Advance(-3)
				.SetAndAdvance(OpCodes.Ldfld, AccessTools.Field(typeof(MenuElementHover), nameof(MenuElementHover.menuSelectableElement)))
				.Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MenuPatches), nameof(MenuIDorEmpty))))
				// .Insert(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MenuElementHover), nameof(MenuElementHover.menuSelectableElement))))
				.InstructionEnumeration();
			// foreach (var codeInstruction in code)
			// 	print(codeInstruction);
			
			return code;
		}

		internal static string MenuIDorEmpty(MenuSelectableElement menuSelectableElement) {
			var r = menuSelectableElement != null ? menuSelectableElement.menuID : "";
			// print($"MenuIDorEmpty: {menuSelectableElement} : {r}");
			return r;
		}
		
		[HarmonyPatch(typeof(MenuManager), nameof(MenuManager.PageAddOnTop))]
		[HarmonyPostfix]
		internal static void PageAddOnTop(MenuManager __instance, MenuPageIndex menuPageIndex, ref MenuPage __result) {
			openedOnTop = true;
		}
		
		[HarmonyPatch(typeof(MenuPage), nameof(MenuPage.Update))]
		[HarmonyPostfix]
		internal static void MenuUpdate(MenuPage __instance) {
			// print($"Page is: {__instance.currentPageState}");
			if (__instance.currentPageState != MenuPage.PageState.Active || __instance.addedPageOnTop) return;
			var selectableElements = new List<MenuSelectableElement>(__instance.selectableElements);
			if (MenuManager.instance.addedPagesOnTop.Count > 0)
				foreach (var page in MenuManager.instance.addedPagesOnTop.Where(page => __instance != page)) {
					selectableElements.AddRange(page.selectableElements);
				}

			if (__instance.menuPageIndex == MenuPageIndex.Saves) {
				var savesPage = __instance.GetComponent<MenuPageSaves>();
				selectableElements.Remove(savesPage.saveFileInfoRow1.GetComponent<MenuSelectableElement>());
				selectableElements.Remove(savesPage.saveFileInfoRow2.GetComponent<MenuSelectableElement>());
				selectableElements.Remove(savesPage.saveFileInfoRow3.GetComponent<MenuSelectableElement>());
				
				selectableElements.Remove(savesPage.saveFileInfoMoonRect.GetComponent<MenuSelectableElement>());
				selectableElements.Remove(savesPage.saveFileInfoMoonText.GetComponent<MenuSelectableElement>());
				selectableElements.Remove(savesPage.saveFileInfoMoonImage.GetComponent<MenuSelectableElement>());
			}
			
			var scrollableMenu = __instance.GetComponentInChildren<MenuScrollBox>();
			if (scrollableMenu == null && MenuManager.instance.addedPagesOnTop.Count > 0) {
				foreach (var page in MenuManager.instance.addedPagesOnTop.Where(page => __instance != page)) {
					scrollableMenu = page.GetComponentInChildren<MenuScrollBox>();
					if (scrollableMenu != null) break;
				}
			}

			if (scrollableMenu != null) {
				foreach (var scroll in scrollableMenu.scrollBar.GetComponentsInChildren<MenuSelectableElement>()) {
					if (scroll == null) continue;
					selectableElements.Remove(scroll);
				}
					
				if (scrollableMenu.scrollBar.TryGetComponent(typeof(MenuSelectableElement), out var a))
					selectableElements.Remove((MenuSelectableElement)a);
			}
			

			foreach (var selectableElement in new List<MenuSelectableElement>(selectableElements).Where(selectableElement => selectableElement)) {
				if (selectableElement.TryGetComponent(typeof(MenuSlider), out var sliderComponent)) {
					var slider = (MenuSlider)sliderComponent;
					var barSize = slider.barSize.GetComponent<MenuSelectableElement>();
					selectableElements.Remove(selectableElement);
					selectableElements.Remove(barSize);
					
				}
				else if (selectableElement.TryGetComponent(typeof(MenuTwoOptions), out _) || selectableElement.TryGetComponent(typeof(CanvasGroup), out var canvas) && ((CanvasGroup)canvas).alpha < 0.5f || !selectableElement.isActiveAndEnabled) {
					selectableElements.Remove(selectableElement);
				}
			}
			
			if (!ActiveButtons.ContainsKey(__instance)) {
				//print($"Adding selected: {selectableElements[0]}");
				ActiveButtons.Add(__instance, selectableElements[0]);
			}
			else if (openedOnTop && MenuManager.instance.addedPagesOnTop.Count > 0 && MenuManager.instance.addedPagesOnTop[0].currentPageState > MenuPage.PageState.Closing) {
				//print($"on top: {MenuManager.instance.addedPagesOnTop[0].selectableElements[0]}");
				ActiveButtons[__instance] = MenuManager.instance.addedPagesOnTop[0].selectableElements[0];
				openedOnTop = false;
			}
			else if (ActiveButtons[__instance] == null || !selectableElements.Contains(ActiveButtons[__instance])) {
				//print($"Resetting selected: {ActiveButtons[__instance]} : {selectableElements[0]}");
				ActiveButtons[__instance] = selectableElements[0];
			}

			if (InputStore.InputDown[InputStore.MenuSelect.DigitalActionHandle] == ButtonState.Pressed && clickCooldown <= 0f) {
				clickCooldown = 0.25f;
				controllerRumbleTime = 0.1f;
				SteamInput.Internal.TriggerVibration(controller.Handle, ushort.MaxValue / 4, ushort.MaxValue / 4);
			}
			
			// print($"ScrollableMenu: {scrollableMenu}");
			
			if (clickCooldown > 0f)
				clickCooldown -= Time.deltaTime;
			else
				UpdateMenuSelect(__instance, scrollableMenu, selectableElements);
			// print(ActiveButtons[__instance].GetComponent<Button>().FindSelectableOnUp());
		}

		[HarmonyPatch(typeof(MenuPage), nameof(MenuPage.StateClosing))]
		[HarmonyPostfix]
		internal static void MenuPageStateClosing(MenuPage __instance) {
			ActiveButtons.Remove(__instance);
		}
		
		
		private static void UpdateMenuSelect(MenuPage menuPage, MenuScrollBox? scrollableMenu, List<MenuSelectableElement> selectableElements) {
			if (InputStore.InputDown[InputStore.MenuUp.DigitalActionHandle] >= ButtonState.Pressed) {
				MoveSelection(Vector2.up, menuPage, scrollableMenu, selectableElements);
			}
			if (InputStore.InputDown[InputStore.MenuDown.DigitalActionHandle] >= ButtonState.Pressed) {
				MoveSelection(Vector2.down, menuPage, scrollableMenu, selectableElements);
			}
			if (InputStore.InputDown[InputStore.MenuRight.DigitalActionHandle] >= ButtonState.Pressed) {
				MoveSelection(Vector2.right, menuPage, scrollableMenu, selectableElements);
			}
			if (InputStore.InputDown[InputStore.MenuLeft.DigitalActionHandle] >= ButtonState.Pressed) {
				MoveSelection(Vector2.left, menuPage, scrollableMenu, selectableElements);
			}
		}

		private static void MoveSelection(Vector2 direction, MenuPage menuPage, MenuScrollBox? scrollableMenu, List<MenuSelectableElement> selectableElements) {
			// List<MenuSelectableElement> selectableElements = menuPage.selectableElements;
			var ActiveButton = ActiveButtons[menuPage];
			var PlayerList = ActiveButton.GetComponentInParent<MenuPlayerListed>();

			if ((direction == Vector2.left || direction == Vector2.right) && PlayerList != null) {
				print("MenuPlayerListed");
				switch (ActiveButton.name) {
					case "Button" when ActiveButton.transform.parent.parent.name == "Player UI Head": // (Steam Profile Button)
						if (direction == Vector2.left) {
							ActiveButton = PlayerList.transform.GetChild(5).GetChild(6).GetComponent<MenuSelectableElement>(); // Menu Button - >
						}
					break;
					case "Menu Button - x":
						if (direction == Vector2.right) {
							ActiveButton = PlayerList.transform.GetChild(5).GetChild(5).GetComponent<MenuSelectableElement>(); // Menu Button - <
						}
					break;
					case "Menu Button - <":
						if (direction == Vector2.left) {
							ActiveButton = PlayerList.transform.GetChild(3).GetChild(0).GetComponent<MenuSelectableElement>(); // Menu Button - x
						}
						else { // right
							ActiveButton = PlayerList.transform.GetChild(5).GetChild(6).GetComponent<MenuSelectableElement>(); // Menu Button - >
						}
					break;
					case "Menu Button - >":
						if (direction == Vector2.left) {
							ActiveButton = PlayerList.transform.GetChild(5).GetChild(5).GetComponent<MenuSelectableElement>(); // Menu Button - <
						}
						else { // right
							ActiveButton = PlayerList.transform.GetChild(2).GetChild(4).GetChild(0).GetComponent<MenuSelectableElement>(); // Button (Steam Profile Button)
						}
					break;
				}
				ActiveButtons[menuPage] = ActiveButton;
	            clickCooldown = 0.25f;
				return;
			}
			
			var ScrollMenuTop = scrollableMenu !=null ? scrollableMenu.scrollerEndPosition : 0f;
			var ScrollMenuEnd = scrollableMenu !=null ? scrollableMenu.scroller.parent.transform.position.y : 0f;
			
			// DebugPrint(direction.ToString());
			GameObject currentGameObject = ActiveButton.gameObject;
			Vector2 currentPos = currentGameObject.transform.position;
			Vector2 menuBoolShift = new Vector2(97f, 0f);
			Vector2 menuSliderShift = new Vector2(21f, 0);
			var currentMenuBool = currentGameObject.GetComponentInParent<MenuTwoOptions>();
			var currentMenuSlider = currentGameObject.GetComponentInParent<MenuSlider>();
			currentGameObject.TryGetComponent(typeof(MenuButton), out var component);
			MenuButton? menuButton = component as MenuButton;
			
			if (currentMenuBool != null) {
				currentPos = (direction.y != 0 ? currentMenuBool.transform.position : currentPos) - menuBoolShift;
			} else if (currentMenuSlider != null) {
				currentPos =  (Vector2)currentMenuSlider.transform.position + (menuButton != null && menuButton.buttonText.text == ">" ? menuSliderShift : Vector2.zero);
			}
			Vector2 localCurrentPos;
			if (currentMenuBool != null) {
				localCurrentPos = (Vector2)(direction.y != 0 ? currentMenuBool.transform.position : currentGameObject.transform.position) - currentPos - menuBoolShift;
			} else if (currentMenuSlider != null) {
				localCurrentPos = (Vector2)currentMenuSlider.transform.position - currentPos + (menuButton != null && menuButton.buttonText.text == ">" ? menuSliderShift : Vector2.zero);
			} else 
				localCurrentPos = (Vector2)currentGameObject.transform.position - currentPos;
			
			// string name = currentMenuBool != null ? currentMenuBool.name + " - " + currentGameObject.GetComponentInChildren<TextMeshProUGUI>().text : currentMenuSlider != null ? currentMenuSlider.name + " - " + currentGameObject.GetComponentInChildren<TextMeshProUGUI>().text : currentGameObject.name;
			// DebugPrint("Active: " + name + ": " + localCurrentPos + " : " + localCurrentPos.normalized);
			
			var candidates = selectableElements.Select((btn, idx) => new { btn, idx, pos = btn.transform.position })
				.Where(x => {
					if (!x.btn.isActiveAndEnabled)
						return false;
					currentGameObject.TryGetComponent(typeof(MenuButton), out var c);
					MenuButton? mb = c as MenuButton;
					var gameObject = x.btn.gameObject;
					var menuBool = gameObject.GetComponentInParent<MenuTwoOptions>();
					var menuSlider = gameObject.GetComponentInParent<MenuSlider>();
					Vector2 toTarget;
					if (menuBool != null) {
						if (!(currentMenuBool != null && menuBool == currentMenuBool)) {
							var b = menuBool.option2TextMesh.GetComponentInParent<MenuSelectableElement>();
							if (x.btn == b) 
								return false;
							toTarget = (Vector2)menuBool.transform.position - menuBoolShift - currentPos;
						}
						else {
							toTarget = (Vector2)(direction.y != 0 ? menuBool.transform.position : x.pos) - currentPos - menuBoolShift;
							// DebugPrint((direction.y != 0 ? "menuBool" : "x.pos") + $" menuBool: {x.pos} : x.pos: {x.pos}");
						}
					} else if (menuSlider != null) {
						if (!(currentMenuSlider != null && menuSlider == currentMenuSlider)) {
							if (mb != null && mb.buttonText.text == ">")
								return false;
							toTarget = (Vector2)menuSlider.transform.position - currentPos ;
						} else {
							// toTarget = (direction.y != 0 ? menuSlider.transform.position : (Vector2)x.pos - menuSliderShift) - currentPos;
							toTarget = (Vector2)menuSlider.transform.position - currentPos + (mb != null && mb.buttonText.text == ">" ? menuSliderShift : Vector2.zero);
							// DebugPrint((direction.y != 0 ? "menuSlider" : "x.pos") + $" menuSlider: {menuSlider.transform.position} : x.pos: {x.pos}");
						}
					}
					else {
						toTarget = (Vector2)x.pos - currentPos;
					}
					
					// string uname = gameObject.name;
					// var textMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>();
					// if (menuBool != null && textMeshProUGUI != null) {
					// 	// print($"menuBool: {menuBool.name} : {gameObject.GetComponentInChildren<TextMeshProUGUI>()}");
					// 	uname = menuBool.name + " - " + textMeshProUGUI.text;
					// }
					// else if (menuSlider != null && textMeshProUGUI != null) {
					// 	// print($"menuSlider: {menuSlider.name} : {gameObject.GetComponentInChildren<TextMeshProUGUI>()}");
					// 	uname = menuSlider.name + " - " + textMeshProUGUI.text;
					// }
					// DebugPrint(uname + ": " + Vector2.Dot(direction, toTarget.normalized) + " : " + toTarget + " : " + toTarget.normalized);
					if ((direction == Vector2.up    && (toTarget.y < localCurrentPos.y || Mathf.Approximately(toTarget.y, localCurrentPos.y))) ||
					    (direction == Vector2.down  && (toTarget.y > localCurrentPos.y || Mathf.Approximately(toTarget.y, localCurrentPos.y))) ||
					    (direction == Vector2.right && (toTarget.x < localCurrentPos.x || Mathf.Approximately(toTarget.x, localCurrentPos.x))) ||
					    (direction == Vector2.left  && (toTarget.x > localCurrentPos.x || Mathf.Approximately(toTarget.x, localCurrentPos.x))))
						return false;
					return Vector2.Dot(direction, toTarget.normalized) > 0.75f && x.btn != ActiveButton;
				}).OrderBy(x => {
					var gameObject = x.btn.gameObject;
					var menuBool = gameObject.GetComponentInParent<MenuTwoOptions>();
					var menuSlider = gameObject.GetComponentInParent<MenuSlider>();
					var dist = Vector2.Distance(currentPos,  menuBool != null ? (Vector2)x.pos - menuBoolShift : menuSlider != null ? menuSlider.transform.position :x.pos);
					// string uname = gameObject.name;
					// var textMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>();
					// if (menuBool != null && textMeshProUGUI != null) {
					// 	// print($"menuBool: {menuBool.name} : {gameObject.GetComponentInChildren<TextMeshProUGUI>()}");
					// 	uname = menuBool.name + " - " + textMeshProUGUI.text;
					// }
					// else if (menuSlider != null && textMeshProUGUI != null) {
					// 	// print($"menuSlider: {menuSlider.name} : {gameObject.GetComponentInChildren<TextMeshProUGUI>()}");
					// 	uname = menuSlider.name + " - " + textMeshProUGUI.text;
					// }
					// DebugPrint("| " + uname + ": " + dist + " : " + currentPos + " : " + x.pos);
					return dist;
				}).ToList();
			
			var next = candidates.FirstOrDefault(c => c.btn != ActiveButton);
			
			if (next != null) {
				ActiveButton = next.btn;
				SteamInput.Internal.Legacy_TriggerHapticPulse(controller.Handle, SteamControllerPad.Left, 20000);
				var gameObject = ActiveButton.gameObject;
				var hangPrevention = 0f;
				// DebugPrint(gameObject.name + ": " + gameObject.transform.position);
				if (scrollableMenu != null && scrollableMenu.isActiveAndEnabled && gameObject.transform.IsChildOf(scrollableMenu.transform)) {
					if (ScrollMenuTop <= gameObject.transform.position.y) {
						while (ScrollMenuTop <= gameObject.transform.position.y) {
							// currentGameObject.TryGetComponent(typeof(MenuButton), out var c);
							// MenuButton? mb = c as MenuButton;
							scrollableMenu.scrollHandleTargetPosition += ActiveButton.rectTransform.rect.height; //.rectTransform.rect.height; //40f;
							scrollableMenu.Update();
							hangPrevention += Time.deltaTime;
							if (hangPrevention > 1f) break;
							//DebugPrint($"possible hanging: up {ScrollMenuTop} <= {gameObject.transform.position.y}");
						}
					} else if (ScrollMenuEnd >= gameObject.transform.position.y) {
						while (ScrollMenuEnd >= gameObject.transform.position.y) {
							scrollableMenu.scrollHandleTargetPosition -= ActiveButton.rectTransform.rect.height; //40f;
							scrollableMenu.Update();
							hangPrevention += Time.deltaTime;
							if (hangPrevention > 1f) break;
							//DebugPrint($"possible hanging: down {ScrollMenuEnd} >= {gameObject.transform.position.y}");
						}
					}
					scrollableMenu.Update();
				}
			}
			ActiveButtons[menuPage] = ActiveButton;
			// print($"Moved: {ActiveButtons[menuPage]} : menu: {menuPage.currentPageState}");
			clickCooldown = 0.25f;
		}
		
		[HarmonyPatch(typeof(MenuPageSettings), nameof(MenuPageSettings.ButtonEventControls))]
		[HarmonyPrefix]
		internal static bool ButtonEventControls() {
			if (!controllerActive) return true;
			SteamInput.Internal.ShowBindingPanel(controller.Handle);
			return false;
		}
	}
	
	private static class InputPatches {

		[HarmonyPatch(typeof(MenuCursor), nameof(MenuCursor.Update))]
		[HarmonyPrefix]
		internal static bool UpdateMenuCursor(MenuCursor __instance) {
			if (!controllerActive) return true;
			if (__instance.mesh.activeSelf) {
				__instance.mesh.gameObject.SetActive(false);
				Cursor.lockState = CursorLockMode.Locked;
			}
			return false;
		}
		
		[HarmonyPatch(typeof(SteamInput), nameof(SteamInput.InitializeInterface))]
		[HarmonyPostfix]
		internal static void InitializeInterfacePatch() {
			SteamInput.Internal.Init(false);
			print("SteamInput Initialized");
		}

		[HarmonyPatch(typeof(SteamManager), nameof(SteamManager.Awake))]
		[HarmonyPostfix]
		private static void AwakePatch() {
			if (InputStore.GlyphSpriteSheet == null) {
				InputStore.GenerateAllGlyphs();

				var gameRoot = Path.GetDirectoryName(Application.dataPath);
				var modPath = Path.GetDirectoryName(Instance.Info.Location);
				var steamRoot = SteamInputHandler.GetSteamRootFromPath(SteamInput.Internal.GetGlyphForActionOrigin_Legacy(InputActionOrigin.SteamController_A));
				var controllerFile = modPath + @"\game_actions_3241660.vdf";
				var controllerConfigs = @$"{steamRoot}\steamapps\common\Steam Controller Configs\{SteamClient.SteamId.AccountId}\config\3241660";
				// print($"{steamRoot}\\controller_config: {Directory.Exists(steamRoot + @"\controller_config")}");
				if (!Directory.Exists(gameRoot + @"\TouchMenuIcons"))
					FileSystem.CopyDirectory(modPath + @"\textures\TouchMenuIcons", gameRoot + @"\TouchMenuIcons");
				if (!Directory.Exists(steamRoot + @"\controller_config"))
					Directory.CreateDirectory(steamRoot + @"\controller_config");
				if (!File.Exists(steamRoot + @"\controller_config\game_actions_3241660.vdf") ||
				    !File.Exists(steamRoot + @"\controller_config\game_actions_3241660.version") ||
				    (File.ReadAllLines(steamRoot + @"\controller_config\game_actions_3241660.version") is { } version && version[0] != Instance.Info.Metadata.Version.ToString())) { 
					File.Copy(controllerFile, steamRoot + @"\controller_config\game_actions_3241660.vdf", true);
					version = [ Instance.Info.Metadata.Version.ToString()];
					File.WriteAllLines(steamRoot + @"\controller_config\game_actions_3241660.version", version);
					MenuManager.instance.PagePopUpScheduled("Heads Up", Color.white, "Controller configs have just been\n(re-)registered with steam.\nYou may need to restart the Game and Steam for them to apply.", "Gotcha", richText: true);
				}
				FileSystem.CopyDirectory(modPath + @"\InputConfigs", controllerConfigs, true);
			}
		}

		[HarmonyPatch(typeof(InputManager), nameof(InputManager.Start))]
		[HarmonyPostfix]
		internal static void StartPatch(InputManager __instance) {
			InputStore.GenerateInputActions(__instance.inputActions);
		}

		[HarmonyPatch(typeof(MenuManager), nameof(MenuManager.StateSet))]
		[HarmonyPostfix]
		[HarmonyPriority(Priority.Last)]
		private static void MenuStateSet(MenuManager.MenuState state) {
			switch (state) {
				case MenuManager.MenuState.Open:
					//controllerLastActionSet = InputDictionary.ActionSetMenuControls;
					SteamInput.Internal.ActivateActionSet(controller.Handle, InputStore.ActionSetMenuControls);
					ActionSetItemRotateLayerActive = false;
					ActionSetItemDistanceLayerActive = false;
					DebugPrint($"State opened: {controller.Id} : {SteamInput.Internal.GetCurrentActionSet(controller.Handle)}");
				break;
				case MenuManager.MenuState.Closed:
					//controllerLastActionSet = PlayerAvatar.instance && PlayerAvatar.instance.spectating && SpectateCamera.instance.currentState != SpectateCamera.State.Head ? InputDictionary.ActionSetSpectatorControls : InputDictionary.ActionSetGameControls;
					SteamInput.Internal.ActivateActionSet(controller.Handle, PlayerAvatar.instance && PlayerAvatar.instance.spectating && SpectateCamera.instance.currentState != SpectateCamera.State.Head ? InputStore.ActionSetSpectatorControls : InputStore.ActionSetGameControls);
					ActionSetItemRotateLayerActive = false;
					ActionSetItemDistanceLayerActive = false;
					DebugPrint($"State closed: {controller.Id} : {SteamInput.Internal.GetCurrentActionSet(controller.Handle)}");
				break;
			}
		}

		[HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.SetSpectate))]
		[HarmonyPostfix]
		internal static void SetSpectate(PlayerAvatar __instance) {
			//controllerLastActionSet = InputDictionary.ActionSetSpectatorControls;
			SteamInput.Internal.ActivateActionSet(controller.Handle, InputStore.ActionSetSpectatorControls);
			DebugPrint($"ActionSet(SetSpectate): {SteamInput.Internal.GetCurrentActionSet(controller.Handle)}");
		}

		[HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.StopSpectate))]
		[HarmonyPostfix]
		internal static void StopSpectate(SpectateCamera __instance) {
			//controllerLastActionSet = InputDictionary.ActionSetGameControls;
			SteamInput.Internal.ActivateActionSet(controller.Handle, InputStore.ActionSetGameControls);
			DebugPrint($"ActionSet(StopSpectate): {SteamInput.Internal.GetCurrentActionSet(controller.Handle)}");
		}
		

		[HarmonyPatch(typeof(ChatManager), nameof(ChatManager.StateActive))]
		[HarmonyPostfix]
		internal static void ChatStateActive() {
			if (MenuManager.instance.currentMenuState != (int)MenuManager.MenuState.Closed && !SteamInput.Internal.GetCurrentActionSet(controller.Handle).Equals(InputStore.ActionSetMenuControls)) return;
			//controllerLastActionSet = InputDictionary.ActionSetMenuControls;
			// if (!ShowedKeyboard) {
			// 	SteamUtils.Internal.ShowFloatingGamepadTextInput(TextInputMode.SingleLine, 0, 0, Screen.width, Screen.height/2);
			// 	ShowedKeyboard = true;
			// }
			SteamInput.Internal.ActivateActionSet(controller.Handle, InputStore.ActionSetMenuControls);
			DebugPrint($"ActionSet(ChatStateActive): {SteamInput.Internal.GetCurrentActionSet(controller.Handle)}");
			ActionSetItemRotateLayerActive = false;
			ActionSetItemDistanceLayerActive = false;
		}
		[HarmonyPatch(typeof(ChatManager), nameof(ChatManager.StateInactive))]
		[HarmonyPostfix]
		internal static void ChatStateInactive() {
			if (MenuManager.instance.currentMenuPage || MenuManager.instance.currentMenuState != (int)MenuManager.MenuState.Closed || SteamInput.Internal.GetCurrentActionSet(controller.Handle).Equals(PlayerAvatar.instance.spectating && SpectateCamera.instance.currentState != SpectateCamera.State.Head ? InputStore.ActionSetSpectatorControls : InputStore.ActionSetGameControls)) return;
			if (!PlayerAvatar.instance.spectating || PlayerAvatar.instance.spectating && InputStore.InputDown[InputStore.ActionInteract.DigitalActionHandle] == ButtonState.Released) {
				var lastSet = SteamInput.Internal.GetCurrentActionSet(controller.Handle);
				SteamInput.Internal.ActivateActionSet(controller.Handle, PlayerAvatar.instance.spectating && SpectateCamera.instance.currentState != SpectateCamera.State.Head ? InputStore.ActionSetSpectatorControls : InputStore.ActionSetGameControls);
				if (lastSet == InputStore.ActionSetSpectatorControls && PlayerAvatar.instance.spectating && SpectateCamera.instance.currentState == SpectateCamera.State.Head) {
					SpectateHeadUI.instance.promptText.text = InputManager.instance.InputDisplayReplaceTags("<color=#FF8C00>Press</color> [interact]", "<color=white>", "</color>");
				}
			}
			// ShowedKeyboard = false;
			DebugPrint($"ActionSet(ChatStateInactive): {SteamInput.Internal.GetCurrentActionSet(controller.Handle)}");
			ActionSetItemRotateLayerActive = false;
			ActionSetItemDistanceLayerActive = false;
		}
		

		[HarmonyPatch(typeof(MenuManager), nameof(MenuManager.PageOpen))]
		[HarmonyPostfix]
		[HarmonyPriority(Priority.Last)]
		internal static void MenuPageOpen() {
			GrabToggle.ConfigFile.Reload();
			
		}

		[HarmonyPatch(typeof(MenuManager), nameof(MenuManager.PageSetCurrent))]
		[HarmonyPostfix]
		[HarmonyPriority(Priority.Last)]
		internal static void MenuPageSetCurrent() {
			GrabToggle.ConfigFile.Reload();
		}
		
		[HarmonyPatch(typeof(InputManager), nameof(InputManager.InputToggleGet))]
		[HarmonyPrefix]
		internal static bool InputToggleGet_Prefix(ref bool __result, InputKey key) {
			__result = !controllerActive || (key == InputKey.Grab && GrabToggle.Value);
			return !controllerActive;
		}
		
		
		[HarmonyPatch(typeof(InputManager), nameof(InputManager.KeyDown))]
		[HarmonyPrefix]
		internal static bool KeyDown_Prefix(InputManager __instance, ref bool __result , InputKey key) {
			if (key is InputKey.Jump or InputKey.Crouch or InputKey.Tumble or InputKey.Inventory1 or InputKey.Inventory2 or InputKey.Inventory3 or InputKey.Interact or InputKey.ToggleMute or InputKey.Expression1 or InputKey.Expression2 or InputKey.Expression3 or InputKey.Expression4 or InputKey.Expression5 or InputKey.Expression6 && __instance.disableMovementTimer > 0f) {
				__result = false;
				return false;
			}
			
			var nowDown = InputStore.InputDown[InputStore.InputNameForSteam[key].DigitalActionHandle] == ButtonState.Pressed;
			if (!nowDown) return true;
			__result = true;
			return false;
		}

		[HarmonyPatch(typeof(InputManager), nameof(InputManager.KeyUp))]
		[HarmonyPrefix]
		internal static bool KeyUp_Prefix(InputManager __instance, ref bool __result, InputKey key) {
			if (key is InputKey.Jump or InputKey.Crouch or InputKey.Tumble && __instance.disableMovementTimer > 0f) {
				__result = false;
				return false;
			}
			
			var nowUp = InputStore.InputDown[InputStore.InputNameForSteam[key].DigitalActionHandle] == ButtonState.Released;
			
			if (!nowUp) return true;
			__result = true;
			return false;
		}
		
		[HarmonyPatch(typeof(InputManager), nameof(InputManager.KeyPullAndPush))]
		[HarmonyPrefix]
		internal static bool KeyPullAndPush_Prefix(InputManager __instance, ref float __result) {
			SteamInput.RunFrame();
			if (SteamInput.Internal.GetAnalogActionData(controller.Handle, InputStore.AnalogActionItemDistance.AnalogActionHandle).Y != 0f) {
				__result = SteamInput.Internal.GetAnalogActionData(controller.Handle, InputStore.AnalogActionItemDistance.AnalogActionHandle).Y;
                return false;
			}
			if (InputStore.InputDown[InputStore.ActionPush.DigitalActionHandle] >= ButtonState.Pressed) {
				__result = 1f;
				return false;
			}
			if (InputStore.InputDown[InputStore.ActionPull.DigitalActionHandle] >= ButtonState.Pressed) {
				__result = -1f;
				return false;
			}
			
			return true;
		}

		[HarmonyPatch(typeof(InputManager), nameof(InputManager.KeyHold))]
		[HarmonyPrefix]
		internal static bool KeyHold_Prefix(InputManager __instance, ref bool __result, InputKey key) {
			if (key is InputKey.Jump or InputKey.Crouch or InputKey.Tumble or InputKey.Expression1 or InputKey.Expression2 or InputKey.Expression3 or InputKey.Expression4 or InputKey.Expression5 or InputKey.Expression6 && __instance.disableMovementTimer > 0f) {
				__result = false;
				return false;
			}
			if (!(InputStore.InputDown[InputStore.InputNameForSteam[key].DigitalActionHandle] > ButtonState.Released)) return true;
			__result = true;
			return false;
		}

		[HarmonyPatch(typeof(InputManager), nameof(InputManager.GetMovementX))]
		[HarmonyPrefix]
		internal static bool GetMovementX_Prefix(InputManager __instance, ref float __result) {
			if (__instance.disableMovementTimer > 0f) {
				__result = 0f;
				return false;
			}
			SteamInput.RunFrame();

			if (SteamInput.Internal.GetAnalogActionData(controller.Handle, InputStore.AnalogActionMove.AnalogActionHandle).X == 0) return true;
			__result = SteamInput.Internal.GetAnalogActionData(controller.Handle, InputStore.AnalogActionMove.AnalogActionHandle).X;
			return false;
		}
		
		[HarmonyPatch(typeof(InputManager), nameof(InputManager.GetMovementY))]
		[HarmonyPrefix]
		internal static bool GetMovementY_Prefix(InputManager __instance, ref float __result) {
			if (__instance.disableMovementTimer > 0f) {
				__result = 0f;
				return false;
			}
			
			SteamInput.RunFrame();

			if (SteamInput.Internal.GetAnalogActionData(controller.Handle, InputStore.AnalogActionMove.AnalogActionHandle).Y == 0) return true;
			__result = SteamInput.Internal.GetAnalogActionData(controller.Handle, InputStore.AnalogActionMove.AnalogActionHandle).Y;
			return false;
		}

		// ReSharper disable once InvertIf
		[HarmonyPatch(typeof(InputManager), nameof(InputManager.GetScrollY))]
		[HarmonyPrefix]
		internal static bool GetScrollY_Prefix(InputManager __instance, ref float __result) {
			SteamInput.RunFrame();
			if (SteamInput.Internal.GetAnalogActionData(controller.Handle, InputStore.AnalogActionZoom.AnalogActionHandle).Y != 0f) {
				__result = SteamInput.Internal.GetAnalogActionData(controller.Handle, InputStore.AnalogActionZoom.AnalogActionHandle).Y * 10;
				return false;
			} 
			if (SteamInput.Internal.GetAnalogActionData(controller.Handle, InputStore.AnalogActionMenuScroll.AnalogActionHandle).Y != 0f) {
				__result = SteamInput.Internal.GetAnalogActionData(controller.Handle, InputStore.AnalogActionMenuScroll.AnalogActionHandle).Y * 10;
				return false;
			}
			return true;
		}

		[HarmonyPatch(typeof(InputManager), nameof(InputManager.GetMovement))]
		[HarmonyPrefix]
		internal static bool GetMovement_Prefix(InputManager __instance, ref Vector2 __result) {
			if (__instance.disableMovementTimer > 0f) {
				__result = Vector2.zero;
				return false;
			}
			SteamInput.RunFrame();
			
			var moveStick = new Vector2(SteamInput.Internal.GetAnalogActionData(controller.Handle, InputStore.AnalogActionMove.AnalogActionHandle).X, SteamInput.Internal.GetAnalogActionData(controller.Handle, InputStore.AnalogActionMove.AnalogActionHandle).Y);
			if (moveStick == Vector2.zero) return true;
			__result = moveStick;
			return false;
		}
		
		[HarmonyPatch(typeof(InputManager), nameof(InputManager.GetMouseX))]
		[HarmonyPrefix]
		internal static bool GetMouseX_Prefix(InputManager __instance, ref float __result) {
			if (__instance.disableAimingTimer > 0f) {
				__result = 0f;
				return false;
			}
			SteamInput.RunFrame();
			if (stickCameraState.X == 0f)
				return true;
			__result = stickCameraState.X * 0.1f;
			return false;
		}
		[HarmonyPatch(typeof(InputManager), nameof(InputManager.GetMouseY))]
		[HarmonyPrefix]
		internal static bool GetMouseY_Prefix(InputManager __instance, ref float __result) {
			if (__instance.disableAimingTimer > 0f) {
				__result = 0f;
				return false;
			}
			SteamInput.RunFrame();
			if (stickCameraState.Y == 0f)
				return true;
			__result = -stickCameraState.Y * 0.1f;
			return false;
		}

		

		[HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.Update))]
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> PhysGrabberUpdate_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il) {
			var pushPull = il.DeclareLocal(typeof(float));
			
			var code = new CodeMatcher(instructions)
				.MatchForward(false, 
					new CodeMatch(OpCodes.Ldsfld, AccessTools.Field(typeof(InputManager), nameof(InputManager.instance))),
					new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(InputManager), nameof(InputManager.KeyPullAndPush)))//,
				)
				.Advance(2)
				.InsertAndAdvance(
					new CodeInstruction(OpCodes.Stloc, pushPull.LocalIndex),
					new CodeInstruction(OpCodes.Ldloc, pushPull.LocalIndex)
				)
				.Advance(21)
				.RemoveInstruction()
				.InsertAndAdvance(
					new CodeInstruction(OpCodes.Ldloc, pushPull.LocalIndex),
					new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(REPO_SteamInput), nameof(PushAndPull)))
				)
				.Advance(3)
				.SetAndAdvance(OpCodes.Ldloc, pushPull.LocalIndex)
				.RemoveInstruction()
				
				.Advance(21)
				.RemoveInstruction()
				.InsertAndAdvance(
					new CodeInstruction(OpCodes.Ldloc, pushPull.LocalIndex),
					new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(REPO_SteamInput), nameof(PushAndPull)))
				)
				.InstructionEnumeration();
			return code;
		}
		
		[HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.ObjectTurning))]
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> ObjectTurningPatches(IEnumerable<CodeInstruction> instructions) {
			return new CodeMatcher(instructions)
				.MatchForward(false, new CodeMatch(OpCodes.Ldstr, "Mouse X"))
				.RemoveInstructions(10)
				.Insert(
					new CodeInstruction(OpCodes.Ldloca_S, (byte)5), // Mouse X
					new CodeInstruction(OpCodes.Ldloca_S, (byte)6), // Mouse Y
					new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(InputPatches), nameof(GetRotationInput)))
				)
				.InstructionEnumeration();
		}

		// ReSharper disable twice RedundantAssignment
		private static void GetRotationInput(ref float x, ref float y) {
			float num2 = Mathf.Lerp(0.2f, 2.5f, GameplayManager.instance.aimSensitivity / 100f);
			x = stickRotateState.X * 0.1f + Input.GetAxis("Mouse X") * num2;
			y = -stickRotateState.Y * 0.1f + Input.GetAxis("Mouse Y") * num2;
		}

		[HarmonyPatch(typeof(PhysGrabber), nameof(PhysGrabber.FixedUpdate))]
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> FixedUpdatePatches(IEnumerable<CodeInstruction> instructions) {
			return new CodeMatcher(instructions)
				.MatchForward(false, new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(SemiFunc), nameof(SemiFunc.CameraOverrideStopAim))))
				.RemoveInstruction()
				.Insert(
					new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(InputPatches), nameof(ShouldOverrideCamera)))
				)
				.InstructionEnumeration();
		}

		private static void ShouldOverrideCamera() {
			if (!controllerActive) SemiFunc.CameraOverrideStopAim();
		}


		[HarmonyPatch(typeof(SplashScreen), nameof(SplashScreen.SkipLogic))]
		[HarmonyPrefix]
		internal static bool SkipLogic_Prefix(SplashScreen __instance) {
			if (__instance.state > SplashScreen.State.Wait && DataDirector.instance.SettingValueFetch(DataDirector.Setting.SplashScreenCount) != 0 && ControllerAnyInput(true)) {
				__instance.StateSet(SplashScreen.State.Done);
				return false;
			}
			return true;
		}
		
		[HarmonyPatch(typeof(CameraShake), nameof(CameraShake.Update))]
		[HarmonyPostfix]
		internal static void CameraShake_Postfix(CameraShake __instance) {
			if (__instance.Strength > 4) {
				controllerRumbleTime = 0.25f;
				SteamInput.Internal.TriggerVibration(controller.Handle, (ushort)(10000 * __instance.Strength - 4), (ushort)(10000 * __instance.Strength - 4));
			}
		}

		[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.ImpactHeavyRPC))]
		[HarmonyPostfix]
		internal static void ImpactHeavy(PhysGrabObjectImpactDetector  __instance) {
			if (!__instance.physGrabObject.heldByLocalPlayer) return;
			controllerRumbleTime = 1f;
			SteamInput.Internal.TriggerVibration(controller.Handle, ushort.MaxValue, ushort.MaxValue);
		}
		
		[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.ImpactMediumRPC))]
		[HarmonyPostfix]
		internal static void ImpactMedium(PhysGrabObjectImpactDetector  __instance) {
			if (!__instance.physGrabObject.heldByLocalPlayer) return;
			controllerRumbleTime = 0.5f;
			SteamInput.Internal.TriggerVibration(controller.Handle, ushort.MaxValue/2, ushort.MaxValue/2);
		}
		
		[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.ImpactLightRPC))]
		[HarmonyPostfix]
		internal static void ImpactLight(PhysGrabObjectImpactDetector  __instance) {
			if (!__instance.physGrabObject.heldByLocalPlayer) return;
			controllerRumbleTime = 0.25f;
			SteamInput.Internal.TriggerVibration(controller.Handle, ushort.MaxValue/3, ushort.MaxValue/3);
		}
		
		
		//Input tips:
		[HarmonyPatch(typeof(InputManager), nameof(InputManager.InputDisplayGet))]
		[HarmonyPrefix]
		private static bool InputDisplayGet(InputManager __instance, InputKey _inputKey, ref string __result) {
			if (!controllerActive) 
				return true;
			var action = __instance.GetAction(_inputKey);
			if (action == null) {
				__result = "missing";
				return false;
			}
			__result = __instance.InputDisplayGetString(action, 0);
			
			return false;
		}
		
		[HarmonyPatch(typeof(InputManager), nameof(InputManager.InputDisplayGetString))]
		[HarmonyPrefix]
		private static bool InputDisplayGetString(InputAction action, ref string __result) {
			SteamInput.RunFrame();
			if (!controllerActive) 
				return true;
			var inputHandle = InputStore.InputActionForSteam![action];
			var returnResult = "";
			
			if (inputHandle.IsDigitalAction) {
				var actionHandles = InputStore.InputForActionSet[inputHandle];
				var actionHandle = actionHandles[0];
				var aH = SteamInput.Internal.GetCurrentActionSet(controller.Handle);
				// print($"ActionSet: {aH}");
				if (actionHandle != aH && actionHandles.Contains(aH)) {
					actionHandle = aH;
				}
				var glyph = SteamInputHandler.GetDigitalGlyphs(actionHandle, inputHandle.DigitalActionHandle);
				if (glyph == "button_none" && InputStore.AnalogDigitalActions.TryGetValue(inputHandle.DigitalActionHandle, out var analogActionHandle)) {
					// AnalogActionHandle
					glyph = SteamInputHandler.GetAnalogGlyphs(actionHandle, analogActionHandle);
				}
				if (glyph == "button_none" && InputStore.ActionLayersForInput.TryGetValue(inputHandle.DigitalActionHandle, out var layer)) {
					var layerGlyph = SteamInputHandler.GetDigitalGlyphs(actionHandle, layer.Item2);
					glyph = SteamInputHandler.GetDigitalGlyphs(layer.Item1, inputHandle.DigitalActionHandle);
					if (glyph == "button_none" && InputStore.RelatedInputsForActionLayer.TryGetValue(layer.Item1, out var inputs)) {
						foreach (var input in inputs) {
							if (input.IsAnalogAction)
								glyph = SteamInputHandler.GetAnalogGlyphs(layer.Item1, input.AnalogActionHandle);
							else if (input.IsDigitalAction)
								glyph = SteamInputHandler.GetDigitalGlyphs(layer.Item1, input.DigitalActionHandle);
							if (glyph != "button_none") break;
						}	
					}
					returnResult = $"<sprite name={layerGlyph}> + ";
				}
				
				returnResult += $"<sprite name={glyph}>";
				// returnResult += $"<{glyph}>";
			} else if (inputHandle.IsAnalogAction) {
				var actionHandles = InputStore.InputForActionSet[inputHandle];
				var actionHandle = actionHandles[0];
				var glyph = SteamInputHandler.GetAnalogGlyphs(actionHandle, inputHandle.AnalogActionHandle);
				if (glyph == "button_none" && InputStore.ActionLayersForSets.TryGetValue(actionHandle, out var layer)) {
					var layerGlyph = "button_none";
					foreach (var set in layer!) {
						glyph = SteamInputHandler.GetAnalogGlyphs(set.Item1, inputHandle.AnalogActionHandle);
						layerGlyph = SteamInputHandler.GetDigitalGlyphs(actionHandle, set.Item2);
					}
					returnResult = $"<sprite name={layerGlyph}> + ";
				}
				returnResult += $"<sprite name={glyph}>";
			}

			__result = returnResult;
			return false;
		}
		
		[HarmonyPatch(typeof(InputManager), nameof(InputManager.InputDisplayReplaceTags))]
		[HarmonyPrefix]
		private static bool NoUnderlinePatch(InputManager __instance, ref string __result, ref string _text) {
			if (!controllerActive) 
				return true;
			_text = __instance.tagDictionary.Aggregate(_text,
				(current, keyValuePair) => current.Replace(keyValuePair.Key,
					__instance.InputDisplayGet(keyValuePair.Value, MenuKeybind.KeyType.InputKey, MovementDirection.Up)));
			__result = _text;
			return false;
		}

		[HarmonyPatch(typeof(ItemInfoUI), nameof(ItemInfoUI.ItemInfoText))]
		[HarmonyPrefix]
		private static void ItemInfoTextPrefix(ItemInfoUI __instance) {
			if (controllerActive) {
				__instance.Text.spriteAsset = InputStore.GlyphSpriteSheet;
				return;
			}
			__instance.Text.spriteAsset = ItemInfoOriginalEmojis;
		}
		
		[HarmonyPatch(typeof(ItemInfoUI), nameof(ItemInfoUI.Start))]
		[HarmonyPostfix]
		private static void ItemInfoUI_Postfix(ItemInfoUI __instance) {
			ItemInfoOriginalEmojis = __instance.Text.spriteAsset; 
		}

		[HarmonyPatch(typeof(SpectateHeadUI), nameof(SpectateHeadUI.Update))]
		[HarmonyPrefix]
		private static void SpectateHeadUI_Update(SpectateHeadUI __instance) {
			if (controllerActive) {
				__instance.promptText.spriteAsset = InputStore.GlyphSpriteSheet;
				return;
			}
			__instance.promptText.spriteAsset = OriginalEmojis;
		}
		

		[HarmonyPatch(typeof(SpectateHeadUI), nameof(SpectateHeadUI.Start))]
		[HarmonyPostfix]
		private static void SpectateHeadUI_Postfix(SpectateHeadUI __instance) {
			OriginalEmojis = __instance.promptText.spriteAsset;
		}
		
		[HarmonyPatch(typeof(MenuPageLobby), nameof(MenuPageLobby.UpdateChatPrompt))]
		[HarmonyPrefix]
		private static void MenuPageLobby_UpdateChatPrompt(MenuPageLobby __instance) {
			if (controllerActive) {
				__instance.chatPromptText.spriteAsset = InputStore.GlyphSpriteSheet;
				return;
			}
			__instance.chatPromptText.spriteAsset = OriginalEmojis;
		}
		
		[HarmonyPatch(typeof(MenuPageLobby), nameof(MenuPageLobby.Start))]
		[HarmonyPostfix]
		private static void MenuPageLobby_Postfix(MenuPageLobby __instance) {
			OriginalEmojis = __instance.chatPromptText.spriteAsset; 
		}
		
		
		//Tutorial: Much the following code was "Inspired™" by RepoXR https://thunderstore.io/c/repo/p/DaXcess/RepoXR/
		[HarmonyPatch(typeof(TutorialUI), nameof(TutorialUI.Start))]
		[HarmonyPostfix]
		private static void OnTutorialStart(TutorialUI __instance) {
			TutorialOriginalEmojis = __instance.Text.spriteAsset;
			if (!controllerActive) 
				return;
			__instance.dummyText.spriteAsset = InputStore.GlyphSpriteSheet;
			__instance.Text.spriteAsset = InputStore.GlyphSpriteSheet;
		}
		[HarmonyPatch(typeof(TutorialUI), nameof(TutorialUI.SetPage))]
		[HarmonyPrefix]
		private static void UpdateTextSpriteAtlas(TutorialUI __instance, ref string dummyTextString, bool transition) {
			if (!controllerActive) {
				__instance.Text.spriteAsset = TutorialOriginalEmojis;
				return;
			}
			__instance.Text.spriteAsset = transition ? TutorialOriginalEmojis : InputStore.GlyphSpriteSheet;
			dummyTextString = dummyTextString.Replace("keyboard", "controller");
		}
		[HarmonyPatch(typeof(TutorialUI), nameof(TutorialUI.SetTipPage))]
		[HarmonyPrefix]
		private static void UpdateTipTextSpriteAtlas(TutorialUI __instance, ref string text) {
			if (!controllerActive) 
				return;
			__instance.Text.spriteAsset = InputStore.GlyphSpriteSheet;
			text = text.Replace("keyboard", "controller");
		}
		
		[HarmonyPatch(typeof(TutorialUI), nameof(TutorialUI.SwitchPage), MethodType.Enumerator)]
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> SwitchPagePatch(IEnumerable<CodeInstruction> instructions) {
			return new CodeMatcher(instructions)
				.MatchForward(false,
					new CodeMatch(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.text))))
				.InsertAndAdvance(
					new CodeInstruction(OpCodes.Ldloc_1),
					new CodeInstruction(OpCodes.Call, ((Action<TutorialUI>)SetSpriteAtlas).Method)
				)
				.InstructionEnumeration();

			static void SetSpriteAtlas(TutorialUI ui) {
				if (!controllerActive) 
					return;
				ui.Text.spriteAsset = InputStore.GlyphSpriteSheet;
			}
		}

		[HarmonyPatch(typeof(TutorialDirector), nameof(TutorialDirector.EndTutorial))]
		[HarmonyPrefix]
		private static void EndTutorial(TutorialDirector __instance) {
			TutorialOriginalEmojis = null!;
		}
	}
}