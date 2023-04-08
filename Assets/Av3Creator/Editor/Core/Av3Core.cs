#region
using System.IO;
using UnityEngine;
using UnityEditor.Animations;
using UnityEditor;
using HarmonyLib;
using System.Reflection;
using System.Linq;
using System;
using System.Collections.Generic;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Avatars.Components;
using Av3Creator.Utils;
using Av3Creator.AdvancedToggles.Modules;
using Av3Creator.AdvancedToggles;
using Av3Creator.Utils.Interactions;
#endregion

namespace Av3Creator.Core
{
    public static class Av3Core
    {
        private static readonly string NEXT_PAGE_GUID = "472ea6a9fb5b0124cb307d74cd50ec09";
        private static Texture2D NextPageTexture = null;

        [InitializeOnLoadMethod]
        static void InitializeReferences()
        {
            if(!string.IsNullOrEmpty(NEXT_PAGE_GUID) && AssetDatabase.GUIDToAssetPath(NEXT_PAGE_GUID) is string NEXT_PAGE_PATH && !string.IsNullOrEmpty(NEXT_PAGE_PATH))
                NextPageTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(NEXT_PAGE_PATH);
            
        }

        public static string RemoveInvalidChars(string filename) => string.Concat(filename.Split(Path.GetInvalidFileNameChars()));

        public static AnimatorController GetVRCLayer(this VRCAvatarDescriptor descriptor, VRCAvatarDescriptor.AnimLayerType layer)
        {
            if (descriptor.baseAnimationLayers.FirstOrDefault(x => x.type == layer) is VRCAvatarDescriptor.CustomAnimLayer myLayer
                && myLayer.animatorController != null && !myLayer.isDefault)
                return (AnimatorController) myLayer.animatorController;
            return null;
        }

        public static void RemoveLayer(this AnimatorController controller, string name)
        {
            for (int i = 0; i < controller.layers.Length; i++)
                if (controller.layers[i].name.Equals(name))
                {
                    controller.RemoveLayer(i);
                    break;
                }
        }

        public static VRCExpressionsMenu.Control AddToMenu(this VRCExpressionsMenu menu, string name, VRCExpressionsMenu.Control.ControlType type, string parameter, string otherParameter = null, Texture2D icon = null)
        {
            if (menu == null) { 
                Debug.LogError("Menu can't be null");
                return null;
            }
            if (menu.controls.Count >= 8)
            {
                Debug.LogError($"Can't add \"{name}\" to menu. Menu is in controls limit.");
                return null;
            }
            var menuControls = menu.controls;
            var control = new VRCExpressionsMenu.Control()
            {
                name = name,
                type = type,
                parameter = new VRCExpressionsMenu.Control.Parameter()
                {
                    name = parameter

                }
            };

            if(!string.IsNullOrEmpty(otherParameter))
            {
                control.subParameters = new VRCExpressionsMenu.Control.Parameter[]
                {
                    new VRCExpressionsMenu.Control.Parameter()
                    {
                        name = otherParameter
                    }
                };
            }

            if (icon != null) control.icon = icon;

            menuControls.Add(control);

            menu.controls = menuControls.ToList();

            EditorUtility.SetDirty(menu);
            return control;
        }


        public static string GetRelativePathFrom(this GameObject obj, GameObject root)
        {
            var path = "";
            if (obj != root)
            {
                path = obj.name;
                Transform parent = obj.transform.parent;
                while (parent != null && parent.gameObject != root)
                {
                    path = parent.name + "/" + path;
                    parent = parent.parent;
                }
            }
            return path;
        }


        public static void FixPath(this GameObject obj, ref string TargetPath)
        {
            if (obj == null)
            {
                TargetPath = "";
                return;
            }

            var descriptor = Av3AdvancedSettings.Instance.VRCAvatarDescriptor;
            if (descriptor != null) TargetPath = obj.GetRelativePathFrom(descriptor.gameObject);
        }

        public static void AddToPaginatorMenu(this VRCExpressionsMenu menu, string menuPath, VRCExpressionsMenu.Control[] controls, int currentPage = 1, bool overwriteFiles = false)
        {
            if (menu == null) throw new Exception("Menu can't be null");
            
            var myControls = controls.ToList();
            var menuControls = menu.controls.ToList();

            foreach(var currentControl in controls)
            {
                int freeSpace = 8 - menuControls.Count;
                if (freeSpace == 1 && myControls.Count > 1)
                {
                    var newMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();

                    var newPath = $"{menuPath} {(currentPage + 1)}.asset";
                    newPath = overwriteFiles ? newPath : AssetDatabase.GenerateUniqueAssetPath(newPath);
                    AssetDatabase.CreateAsset(newMenu, newPath);

                    newMenu.AddToPaginatorMenu(menuPath, myControls.ToArray(), currentPage + 1, overwriteFiles);

                    menuControls.Add(new VRCExpressionsMenu.Control()
                    {
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = newMenu, 
                        name = "Page " + (currentPage + 1),
                        icon = NextPageTexture
                    });
                    break;
                }

                menuControls.Add(currentControl);
                myControls.Remove(currentControl);
            }

            menu.controls = menuControls;
            EditorUtility.SetDirty(menu);
        }


        public static bool AddParameter(this VRCExpressionParameters parameters, string name, VRCExpressionParameters.ValueType type, float defaultValue = 0f, bool saved = true)
        {
            if (parameters.CalcTotalCost() + VRCExpressionParameters.TypeCost(type) > VRCExpressionParameters.MAX_PARAMETER_COST) return false; // cant add, insuficient space
            if (parameters.parameters.Any(x => x.name == name && x.valueType == type)) return true; // already in params, just skip

            try
            {
                var paramList = parameters.parameters.ToList();
                paramList.Add(new VRCExpressionParameters.Parameter()
                {
                    defaultValue = defaultValue,
                    name = name,
                    saved = saved,
                    valueType = type
                });
                parameters.parameters = paramList.ToArray();
                EditorUtility.SetDirty(parameters);
            }
            catch
            {
                return false;
            }
            return true;
        }

        public static void AddEmptyParameter(this VRCExpressionParameters parameters, VRCExpressionParameters.ValueType type)
        {
            var paramList = parameters.parameters.ToList();
            paramList.Add(new VRCExpressionParameters.Parameter()
            {
                defaultValue = 0f,
                name = "",
                saved = true,
                valueType = type
            });
            parameters.parameters = paramList.ToArray();

            EditorUtility.SetDirty(parameters);
        }

        public static void ClearAll(this VRCExpressionParameters parameters)
        {
            if (EditorUtility.DisplayDialog("Clear Parameters?", "Do you really want to clear all parameters?", "Yes", "No"))
            {
                var paramList = parameters.parameters.ToList();
                paramList.Clear();
                parameters.parameters = paramList.ToArray();

                EditorUtility.SetDirty(parameters);
            }
        }

        public static void ResetToDefault(this VRCExpressionParameters parameters)
        {
            if (EditorUtility.DisplayDialog("Reset to Default Parameters?", "Do you really want to reset all parameters to default?", "Yes", "No"))
            {
                var paramList = parameters.parameters.ToList();
                paramList.Clear();

                paramList.Add(new VRCExpressionParameters.Parameter()
                {
                    name = "VRCEmote",
                    valueType = VRCExpressionParameters.ValueType.Int
                });

                paramList.Add(new VRCExpressionParameters.Parameter()
                {
                    name = "VRCFaceBlendH",
                    valueType = VRCExpressionParameters.ValueType.Float
                });

                paramList.Add(new VRCExpressionParameters.Parameter()
                {
                    name = "VRCFaceBlendV",
                    valueType = VRCExpressionParameters.ValueType.Float
                });

                Undo.RecordObject(parameters, "Av3Creator - Reset Params to Default");
                parameters.parameters = paramList.ToArray();

                EditorUtility.SetDirty(parameters);
            }
        }

        //private static readonly string VRCAvatarHandsLayer = "Assets/VRCSDK/Examples3/Animation/Controllers/vrc_AvatarV3HandsLayer.controller";
        private static readonly string VRCAvatarHandsLayerGUID = "404d228aeae421f4590305bc4cdaba16";

        public static void CreateFXLayer(this VRCAvatarDescriptor descriptor, string path)
        {
            if (descriptor == null) return;

            Directory.CreateDirectory(Path.Combine(path, "Layers"));
            path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(path, "Layers/FX.controller"));

            AnimatorController fxController = new AnimatorController();

            var VRCAvatarHandsLayer = AssetDatabase.GUIDToAssetPath(VRCAvatarHandsLayerGUID);
            if (!string.IsNullOrEmpty(VRCAvatarHandsLayerGUID) && AssetDatabase.LoadAssetAtPath<AnimatorController>(VRCAvatarHandsLayer) != null)
            {
                AssetDatabase.CopyAsset(VRCAvatarHandsLayer, path);
                fxController = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            }
            else
            {
                AssetDatabase.CreateAsset(fxController, path);
                fxController.AddLayer("Main Layer");
            }

            descriptor.customizeAnimationLayers = true;

            var fxLayer = descriptor.baseAnimationLayers.First(x => x.type == VRCAvatarDescriptor.AnimLayerType.FX);
            var index = Array.IndexOf(descriptor.baseAnimationLayers, fxLayer);
            descriptor.baseAnimationLayers[index].isDefault = false;
            descriptor.baseAnimationLayers[index].animatorController = fxController;

            EditorUtility.SetDirty(fxController);
            EditorUtility.SetDirty(descriptor);
        }

        public static void CreateParameters(this VRCAvatarDescriptor descriptor, string path)
        {
            Directory.CreateDirectory(path);

            var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            parameters.parameters = new VRCExpressionParameters.Parameter[0];
            path = AssetDatabase.GenerateUniqueAssetPath(path + "/Parameters.asset");
            AssetDatabase.CreateAsset(parameters, path);
            descriptor.customExpressions = true;
            descriptor.expressionParameters = parameters;

            EditorUtility.SetDirty(parameters);
        }

        public static void CreateMainMenu(this VRCAvatarDescriptor descriptor, string path)
        {
            Directory.CreateDirectory(path);

            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            path = AssetDatabase.GenerateUniqueAssetPath(path + "/Main Menu.asset");
            AssetDatabase.CreateAsset(menu, path);
            descriptor.customExpressions = true;
            descriptor.expressionsMenu = menu;

            EditorUtility.SetDirty(menu);
        }

        public static void FixExpressions(this VRCAvatarDescriptor descriptor)
        {
            //var fxLayer = (AnimatorController)descriptor?.baseAnimationLayers[Av3Layers.FX].animatorController;
            var fxLayer = descriptor?.GetVRCLayer(VRCAvatarDescriptor.AnimLayerType.FX);
           
            if (fxLayer == null)
            {
                EditorUtility.DisplayDialog("ERROR: Missing FX Layer", "Your avatar need to have a FX Layer.", "Ok");
                return;
            }
            Undo.RecordObject(fxLayer, "Av3Creator - Remove Fix Expressions");


            void FixLayer(bool isRightHand)
            {
                var layerName = isRightHand ? "Right Hand" : "Left Hand";
                var layer = fxLayer.layers.First(x => x.name == layerName);
                if (layer == null)
                {
                    Debug.Log($"ERROR: Can't find \"{layerName}\". Make sure your avatar have both hands layers.");
                    return;
                }

                foreach (var transition in layer.stateMachine.anyStateTransitions)
                {
                    var conditionToAdd = isRightHand ? "GestureLeft" : "GestureRight";
                    var currentCondition = transition.conditions.First(x => x.parameter == (isRightHand ? "GestureRight" : "GestureLeft"));
                    if (!transition.conditions.Any(x => x.parameter == conditionToAdd) && currentCondition.threshold > 0)
                        transition.AddCondition(AnimatorConditionMode.Equals, 0, conditionToAdd);
                }
            }

            FixLayer(true);
            FixLayer(false);

            EditorUtility.SetDirty(fxLayer);
        }

        public static void RemoveFixExpression(this VRCAvatarDescriptor descriptor)
        {
            //var fxLayer = (AnimatorController)descriptor?.baseAnimationLayers[Av3Layers.FX].animatorController;
            var fxLayer = descriptor?.GetVRCLayer(VRCAvatarDescriptor.AnimLayerType.FX);
            if (fxLayer == null)
            {
                EditorUtility.DisplayDialog("ERROR: Missing FX Layer", "Your avatar need to have a FX Layer.", "Ok");
                return;
            }
            Undo.RecordObject(fxLayer, "Av3Creator - Fix Expressions");

            void RemoveFix(bool isRightHand)
            {
                var layerName = isRightHand ? "Right Hand" : "Left Hand";
                var layer = fxLayer.layers.First(x => x.name == layerName);
                if (layer == null)
                {
                    Debug.Log($"ERROR: Can't find \"{layerName}\". Make sure your avatar have both hands layers.");
                    return;
                }

                foreach (var transition in layer.stateMachine.anyStateTransitions)
                {
                    var conditionToAdd = isRightHand ? "GestureLeft" : "GestureRight";
                    var currentCondition = transition.conditions.First(x => x.parameter == (isRightHand ? "GestureRight" : "GestureLeft"));
                    if (transition.conditions.Any(x => x.parameter == conditionToAdd))
                        transition.RemoveCondition(transition.conditions.First(x => x.parameter == conditionToAdd));
                }
            }

            RemoveFix(true);
            RemoveFix(false);

            EditorUtility.SetDirty(fxLayer);
        }

        public static void FixExpressions2(this VRCAvatarDescriptor descriptor, string outputDirectory)
        {
            if (descriptor == null || string.IsNullOrEmpty(outputDirectory)) return;

            //var fxLayer = (AnimatorController)descriptor?.baseAnimationLayers[Av3Layers.FX].animatorController;
            var fxLayer = descriptor?.GetVRCLayer(VRCAvatarDescriptor.AnimLayerType.FX);
            if (fxLayer == null)
            {
                EditorUtility.DisplayDialog("ERROR: Missing FX Layer", "Your avatar need to have a FX Layer.", "Ok");
                return;
            }
            Undo.RecordObject(fxLayer, "Av3Creator - Remove Fix Expressions");


            var blendshapes = new List<(string propertyPath, string blendshapeName)>();
            var alreadyFixedAnims = new List<(AnimationClip oldAnimation, AnimationClip newAnimation)>();
            void FixLayer(bool isRightHand)
            {
                var layerName = isRightHand ? "Right Hand" : "Left Hand";
                var layer = fxLayer.layers.First(x => x.name == layerName);
                if (layer == null)
                {
                    Debug.Log($"ERROR: Can't find \"{layerName}\". Make sure your avatar have both hands layers.");
                    return;
                }

                var allClips = layer.stateMachine.anyStateTransitions.Select(x => x.destinationState.motion).Distinct();
                foreach (AnimationClip clip in allClips)
                {
                    EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);

                    for (int i = 0; i < curveBindings.Length; i++)
                    {
                        var binding = curveBindings[i];
                        var curve = AnimationUtility.GetEditorCurve(clip, curveBindings[i]);
                        if (binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape."))
                        {
                            var blendshapeName = binding.propertyName.Substring(11);
                            blendshapes.Add((binding.path, blendshapeName));
                        }
                    }
                }
                blendshapes = blendshapes.Distinct().ToList();

                //List<AnimatorState> alreadyFixed = new List<AnimatorState>();
                foreach (var transition in layer.stateMachine.anyStateTransitions)
                {
                    //if (alreadyFixed.Contains(transition.destinationState)) continue;
                    
                    //alreadyFixed.Add(transition.destinationState);

                    var oldAnimation = transition.destinationState.motion as AnimationClip;
                    var newAnimation = new AnimationClip();

                    if (!alreadyFixedAnims.Any(x => x.oldAnimation == oldAnimation))
                    {
                        //Copy
                        AnimationUtility.SetAnimationClipSettings(newAnimation, AnimationUtility.GetAnimationClipSettings(oldAnimation));
                        newAnimation.frameRate = oldAnimation.frameRate;

                        EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(oldAnimation);

                        for (int i = 0; i < curveBindings.Length; i++)
                            AnimationUtility.SetEditorCurve(newAnimation, curveBindings[i], AnimationUtility.GetEditorCurve(oldAnimation, curveBindings[i]));

                        foreach (var (path, blendshape) in blendshapes)
                        {
                            if (!curveBindings.Any(x => x.path == path && x.propertyName == "blendShape." + blendshape))
                            {
                                var renderer = descriptor.gameObject.transform.Find(path)?.GetComponent<SkinnedMeshRenderer>();

                                var blendshapeOriginalValue = renderer == null ? 0 : renderer.GetBlendShapeWeight(renderer.sharedMesh.GetBlendShapeIndex(blendshape));
                                var curve = new AnimationCurve(new Keyframe(0, blendshapeOriginalValue));
                                newAnimation.SetCurve(path, typeof(SkinnedMeshRenderer), "blendShape." + blendshape, curve);
                            }
                        }
                    }
                    else
                        newAnimation = alreadyFixedAnims.First(x => x.oldAnimation == oldAnimation).newAnimation;

                    alreadyFixedAnims.Add((oldAnimation, newAnimation));
                    

                    Directory.CreateDirectory(outputDirectory + "/Expressions/");
                    var assetPath = AssetDatabase.GenerateUniqueAssetPath(outputDirectory + "/Expressions/" + oldAnimation.name + ".anim");
                    AssetDatabase.CreateAsset(newAnimation, assetPath);

                    if (!transition.conditions.Any(x => x.threshold == 0))
                    {
                        transition.destinationState.motion = newAnimation;
                        EditorUtility.SetDirty(transition.destinationState);
                    }
                }
            }

            FixLayer(true);
            FixLayer(false);

            fxLayer.AddLayer("Reset Hand");
            var layers = fxLayer.layers.ToList();
            var lastLayer = layers.Last();
            lastLayer.defaultWeight = 1f;

            if (AssetDatabase.GUIDToAssetPath("b2b8bad9583e56a46a3e21795e96ad92") is string avatarMaskPath && !string.IsNullOrEmpty(avatarMaskPath) 
                && AssetDatabase.LoadAssetAtPath<AvatarMask>(avatarMaskPath) is AvatarMask avatarMask && avatarMask != null)
                lastLayer.avatarMask = avatarMask;

            layers.RemoveAt(layers.IndexOf(lastLayer));
            layers.Insert(1, lastLayer);

            // reposicionate states
            lastLayer.stateMachine.entryPosition = lastLayer.stateMachine.anyStatePosition + new Vector3(0, -10);
            lastLayer.stateMachine.anyStatePosition = lastLayer.stateMachine.entryPosition + new Vector3(0, 40);
            lastLayer.stateMachine.exitPosition = lastLayer.stateMachine.anyStatePosition + new Vector3(0, 40);

            var entryPos = lastLayer.stateMachine.entryPosition;
            var waitingState = lastLayer.stateMachine.AddState("Waiting to Reset", new Vector3(entryPos.x + 200, entryPos.y));
            waitingState.motion = Av3Utils.GetOrCreateEmptyAnimation(outputDirectory);
            lastLayer.stateMachine.defaultState = waitingState;

            var resetState = lastLayer.stateMachine.AddState("Reset", new Vector3(entryPos.x + 200, entryPos.y + 50));
            AnimationClip resetClip = new AnimationClip();
            foreach(var (propertyPath, blendshapeName) in blendshapes)
            {
                var curve = new AnimationCurve(new Keyframe(0, 0));
                resetClip.SetCurve(propertyPath, typeof(SkinnedMeshRenderer), "blendShape." + blendshapeName, curve);
            }
            resetState.motion = resetClip;

            //Create Transitions
            var waitingToReset = waitingState.AddTransition(resetState);
            waitingToReset.canTransitionToSelf = false;
            waitingToReset.duration = 0;
            waitingToReset.AddCondition(AnimatorConditionMode.Equals, 0, "GestureLeft");
            waitingToReset.AddCondition(AnimatorConditionMode.Equals, 0, "GestureRight");
            EditorUtility.SetDirty(waitingToReset);

            var resetToWaiting = resetState.AddTransition(waitingState);
            resetToWaiting.canTransitionToSelf = false;
            resetToWaiting.duration = 0;
            resetToWaiting.AddCondition(AnimatorConditionMode.NotEqual, 0, "GestureLeft");
            resetToWaiting.AddCondition(AnimatorConditionMode.NotEqual, 0, "GestureRight");
            EditorUtility.SetDirty(resetToWaiting);


            var resetPath = outputDirectory + "/Expressions/Reset Hand Shapes.anim";
            AssetDatabase.CreateAsset(resetClip, resetPath);
            EditorUtility.SetDirty(resetClip);


            fxLayer.layers = layers.ToArray();
            EditorUtility.SetDirty(fxLayer);
        }

        public static bool ContainsParameter(AnimatorTransitionBase[] transitions, string conditionName)
        {
            if (transitions == null || transitions.Length == 0) return false;

            bool containsParameter = false;
            foreach (var transition in transitions)
            {
                if (transition.conditions.Any(x => x.parameter == conditionName))
                {
                    containsParameter = true;
                    break;
                }
            }
            return containsParameter;
        }
        public static bool LayerContainsParameter(AnimatorStateMachine stateMachine, string conditionName)
        {
            if (stateMachine == null) return false;
            if (ContainsParameter(stateMachine.anyStateTransitions, conditionName)) return true;
            if (ContainsParameter(stateMachine.entryTransitions, conditionName)) return true;
            if (ContainsParameter(stateMachine.defaultState?.transitions, conditionName)) return true;

            bool containsParameter = false;
            
            foreach (var currentState in stateMachine.states)
            {
                if(currentState.state.motion is BlendTree blendTree && (blendTree.blendParameter == conditionName || blendTree.blendParameterY == conditionName))
                {
                    containsParameter = true;
                    break;
                }

                if (currentState.state.cycleOffsetParameter == conditionName
                    || currentState.state.mirrorParameter == conditionName
                    || currentState.state.speedParameter == conditionName
                    || currentState.state.timeParameter == conditionName)
                {
                    containsParameter = true;
                    break;
                }

                if (ContainsParameter(currentState.state.transitions, conditionName))
                {
                    containsParameter = true;
                    break;
                }
            }
            if (containsParameter) return true;

            foreach (var subStateMachine in stateMachine.stateMachines)
                if (LayerContainsParameter(subStateMachine.stateMachine, conditionName))
                {
                    containsParameter = true;
                    break;
                }

            return containsParameter;
        }

        public static void RemoveUnusedParams(this VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null || descriptor.expressionParameters == null || descriptor.expressionParameters.parameters == null) return;

            var layers = descriptor.baseAnimationLayers.ToList();
            layers.AddRange(descriptor.specialAnimationLayers);

            foreach(var layer in layers)
            {
                if (!layer.isDefault && layer.animatorController is AnimatorController controller && controller != null)
                {
                    var newParameters = controller.parameters.Where(x => controller.layers.Any(y => LayerContainsParameter(y.stateMachine, x.name)));
                    Undo.RecordObject(controller, "Av3Creator - Cleaned Unused Params");
                    controller.parameters = newParameters.ToArray();
                    EditorUtility.SetDirty(controller);
                }
            }

            var onlyUsedParameters = descriptor.expressionParameters.parameters.Where(x => layers.Any(layer =>
           !layer.isDefault && layer.animatorController is AnimatorController controller && controller != null && controller.layers.Any(y => LayerContainsParameter(y.stateMachine, x.name)))).ToArray();

            var totalSizeLibered = descriptor.expressionParameters.parameters.Where(x => !onlyUsedParameters.Contains(x)).Aggregate(0, (total, param) => total + VRCExpressionParameters.TypeCost(param.valueType));

            Debug.Log($"Removed {(descriptor.expressionParameters.parameters.Length - onlyUsedParameters.Length)} parameter(s). (+{totalSizeLibered} free bytes)");
            Undo.RecordObject(descriptor.expressionParameters, "Av3Creator - Cleaned Unused Params");
            descriptor.expressionParameters.parameters = onlyUsedParameters;
            EditorUtility.SetDirty(descriptor.expressionParameters);
        }

        public static void RemoveDuplicatedParams(this VRCAvatarDescriptor descriptor)
        {
            if (descriptor != null && descriptor.expressionParameters != null)
            {
                Undo.RecordObject(descriptor.expressionParameters, "Av3Creator - Remove Duplicated Parameters");

                var newList = descriptor.expressionParameters.parameters.ToList();
                descriptor.expressionParameters.parameters = newList.Aggregate(new List<VRCExpressionParameters.Parameter>(),
                (myList, currentParameter) =>
                {
                    if (!myList.Any(x => x.name == currentParameter.name && x.valueType == currentParameter.valueType))
                        myList.Add(currentParameter);
                    return myList;
                }).ToArray();

                EditorUtility.SetDirty(descriptor.expressionParameters);
            }
        }
    }

   

    [InitializeOnLoad, ExecuteInEditMode]
    public class Av3Patches
    {
        private static HarmonyMethod GetPatch(string name) => new HarmonyMethod(AccessTools.Method(typeof(Av3Patches), name));

        static Av3Patches() => Initialize();

        private static readonly Type SceneHierarchy = AccessTools.TypeByName("UnityEditor.SceneHierarchy");

        internal static bool TryToPatch(Harmony harmony, MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null)
        {
            try
            {
                harmony.Patch(original, prefix, postfix);
                return true;
            }
            catch(Exception error)
            {
                Debug.LogError("Error on patching\n" + error);
                return false;
            }
        }

        private static void Initialize()
        {
            if (!EditorPrefs.GetBool("Av3Creator_V1_2"))
            {
                if(EditorUtility.DisplayDialog("Av3Creator V1.2.0 - READ THIS!", 
                    "Thank y'all for the amazing support!\n" +
                    "This updates comes with a lot of new features and improvements, you can see the complete patch notes in our discord.\n\n" +
                    "Remember to give credits to Av3Creator when publish your avatar and is NOT NEEDED to add Av3Creator in your unitypackage! This will be considered LEAKING!", 
                    "Join Discord", "Close"))
                    Application.OpenURL("https://discord.gg/3ZpeG5yahd");
                
                EditorPrefs.SetBool("Av3Creator_V1_2", true);
            }

            try
            {
                var harmony = new Harmony("Av3Creator_Patcher");

                TryToPatch(harmony, typeof(UnityEditorInternal.InternalEditorUtility)
                    .GetMethod("IsScriptOrAssembly", AccessTools.all), GetPatch(nameof(Av3Patch)));

                TryToPatch(harmony, typeof(EditorGUI).GetMethod("FillPropertyContextMenu", AccessTools.all),
                    postfix: GetPatch(nameof(PropertyInterceptor)));

                TryToPatch(harmony, AccessTools.Method(SceneHierarchy, "AddCreateGameObjectItemsToMenu"),
                prefix: GetPatch(nameof(GameObjectInterceptor)));

            }
            catch
            {
               
                // Just ignore the errors. Cause can be caused by multiple fators.
            }
        }

        public static void GameObjectInterceptor(GenericMenu menu)
        {
            var selection = Selection.GetFiltered<GameObject>(SelectionMode.Editable);
            if (selection == null || selection.Length <= 0) return;
            var vrcDescriptor = selection[0].GetComponentInParent<VRCAvatarDescriptor>();
            if (vrcDescriptor == null || selection[0].GetComponent<VRCAvatarDescriptor>() == vrcDescriptor) return;
            bool hasRenderer = Selection.GetFiltered<Renderer>(SelectionMode.Editable).Length > 0;

            var Instance = Av3AdvancedSettings.Instance;
            menu.AddItem(new GUIContent("Av3Creator Advanced Toggle/New Toggle/Empty Toggle"), false, () =>
            {
                Instance.VRCAvatarDescriptor = vrcDescriptor;
                foreach (var selected in selection)
                {
                    if (selected == null || selected.GetComponent<VRCAvatarDescriptor>() == vrcDescriptor) continue;

                    var toggle = new Av3AdvancedToggle()
                    {
                        Name = selected.name,
                        IsExpanded = selection.Length == 1
                    };

                    toggle.GenerateParameterName();
                    Instance.AdvancedToggles.Add(toggle);
                    Av3AdvancedSettings.Save();
                }
            });

            menu.AddItem(new GUIContent("Av3Creator Advanced Toggle/New Toggle/Toggle Object"), false, () =>
            {
                Instance.VRCAvatarDescriptor = vrcDescriptor;
                foreach (var selected in selection)
                {
                    if (selected == null || selected.GetComponent<VRCAvatarDescriptor>() == vrcDescriptor) continue;
                    var module = new Av3AdvancedToggleObject()
                    {
                        Target = selected,
                        ToggleState = true
                    };

                    var myToggle = new Av3AdvancedToggle()
                    {
                        Name = selected.name,
                        Modules = new List<IAv3AdvancedModule>()
                        {
                            module
                        },
                        IsExpanded = selection.Length == 1,
                        DefaultValue = selected.activeSelf
                    };
                    Instance.AdvancedToggles.Add(myToggle);
                    myToggle.GenerateParameterName();
                    module.InitializeModule();
                }

                Av3AdvancedSettings.Save();
            });

            if (hasRenderer)
            {
                menu.AddItem(new GUIContent("Av3Creator Advanced Toggle/New Toggle/Poiyomi Dissolve"), false, () =>
                {
                    foreach (var selected in selection)
                    {
                        if (selected == null)
                            continue;
                        
                        if(selected.GetComponent<VRCAvatarDescriptor>() == vrcDescriptor)
                        {
                            Debug.LogWarning("Av3Creator: Selected object is the root Avatar Descriptor and can't be added to toggles!");
                            continue;
                        }

                        var renderer = selected.GetComponent<Renderer>();
                        if (renderer == null)
                        {
                            Debug.LogWarning("Av3Creator: Selected object don't have a renderer component");
                            continue;
                        }

                        var materials = renderer.sharedMaterials.Where(x => x.IsPoiyomi()).Distinct().ToList();
                        if (materials == null || materials.Count == 0)
                        {
                            Debug.LogWarning("Av3Creator: Selected object don't have any Poiyomi material. Make sure that you are using Poiyomi V7.3.X to create Dissolve Toggles.");
                            continue;
                        }
                        Instance.VRCAvatarDescriptor = vrcDescriptor;

                        var module = new Av3AdvancedObjectDissolve()
                        {
                            TargetRenderer = renderer,
                            SelectedMaterials = materials
                        };

                        var myToggle = new Av3AdvancedToggle()
                        {
                            Name = selected.name,
                            Modules = new List<IAv3AdvancedModule>()
                            {
                                module
                            },
                            IsExpanded = selection.Length == 1,
                            DefaultValue = selected.activeSelf
                        };
                        Instance.AdvancedToggles.Add(myToggle);
                        myToggle.GenerateParameterName();
                        module.InitializeModule();            
                    }

                    Av3AdvancedSettings.Save();
                });
            }

            if (Instance.AdvancedToggles.Count > 0)
            {
                foreach (var toggle in Instance.AdvancedToggles)
                {
                    if (toggle == null) continue;
                    if (string.IsNullOrEmpty(toggle.Name)) continue;

                    menu.AddItem(new GUIContent("Av3Creator Advanced Toggle/Existing/" + toggle.Name + "/Toggle Object"), false, () =>
                   {
                       foreach (var selected in selection)
                       {
                           if (selected == null || selected.GetComponent<VRCAvatarDescriptor>() == vrcDescriptor) continue;
                           if (toggle.Modules == null) toggle.Modules = new List<IAv3AdvancedModule>();

                           var module = new Av3AdvancedToggleObject()
                           {
                               Target = selected,
                               ToggleState = true
                           };
                           toggle.Modules.Add(module);
                           toggle.IsExpanded = true;
                           toggle.ModulesExpanded = true;
                           module.InitializeModule();
                       }

                       Av3AdvancedSettings.Save();
                   });

                    if (hasRenderer)
                    {
                        menu.AddItem(new GUIContent("Av3Creator Advanced Toggle/Existing/" + toggle.Name + "/Poiyomi Dissolve"), false, () =>
                        {
                            foreach (var selected in selection)
                            {
                                if (selected == null || selected.GetComponent<VRCAvatarDescriptor>() == vrcDescriptor) continue;

                                var renderer = selected.GetComponent<Renderer>();
                                if (renderer == null) continue;
                                var materials = renderer.sharedMaterials.Where(x => x.IsPoiyomi()).Distinct().ToList();
                                if (materials == null || materials.Count == 0) continue;
                                
                                if (toggle.Modules == null) toggle.Modules = new List<IAv3AdvancedModule>();

                                var module = new Av3AdvancedObjectDissolve()
                                {
                                    TargetRenderer = renderer,
                                    SelectedMaterials = materials
                                };
                                toggle.Modules.Add(module);
                                toggle.IsExpanded = true;
                                toggle.ModulesExpanded = true;
                                module.InitializeModule();            
                            }

                            Av3AdvancedSettings.Save();
                        });
                    }
                }
            }
        }

        internal static void PropertyInterceptor(ref GenericMenu __result, SerializedProperty property)
        {
            try
            {
                var targetObject = property?.serializedObject?.targetObject;
                var targetName = property.propertyPath.Split('.')[0];
                var arrayIndex = -1;

                if (property.propertyPath.Contains(".Array.data[") && property.propertyPath.LastIndexOf(']') == property.propertyPath.Length - 1)
                {
                    var parentArrayIndexString = property.propertyPath.Substring(property.propertyPath.LastIndexOf(".Array.data[", StringComparison.Ordinal) + 12);
                    parentArrayIndexString = parentArrayIndexString.Substring(0, parentArrayIndexString.IndexOf("]"));
                    arrayIndex = int.Parse(parentArrayIndexString);
                }

                __result.allowDuplicateNames = false;
                if (arrayIndex >= 0 && targetObject != null && targetObject is SkinnedMeshRenderer && targetName == "m_BlendShapeWeights")
                {
                    var meshRenderer = targetObject as SkinnedMeshRenderer;
                    var blendShapeName = meshRenderer.sharedMesh.GetBlendShapeName(arrayIndex);
                    var blendShapeValue = meshRenderer.GetBlendShapeWeight(arrayIndex);

                    var vrcDescriptor = meshRenderer.gameObject.GetComponentInParent<VRCAvatarDescriptor>();
                    if (vrcDescriptor == null) return;

                    var instance = Av3AdvancedSettings.Instance;

                    __result.AddDisabledItem(new GUIContent("Selected: " + blendShapeName));

                    __result.AddItem(new GUIContent("Av3Creator New Toggle/Custom Blendshape"), false, () =>
                    {
                        var window = EditorWindow.GetWindow<Av3BlendshapeCreator>(utility: true, title: "Blendshape Creator: " + blendShapeName, focus: true);
                        window.minSize = new Vector2(300, 100);
                        window.maxSize = window.minSize + new Vector2(100, 0);
                        window.SetParameter(meshRenderer, new Av3Blendshape(arrayIndex, blendShapeName, 0, 100));
                        window.ShowPopup();
                    });

                    __result.AddItem(new GUIContent("Av3Creator New Toggle/Blendshape/0 -> 100"), false, () =>
                    {
                        Av3AdvancedSettings.AddBlendshape(meshRenderer, arrayIndex, false, 0, 100);
                    });

                    __result.AddItem(new GUIContent("Av3Creator New Toggle/Blendshape/100 -> 0"), false, () =>
                    {
                        Av3AdvancedSettings.AddBlendshape(meshRenderer, arrayIndex, false, 100, 0);
                    });

                    __result.AddItem(new GUIContent("Av3Creator New Toggle/Animated Blendshape/0 -> 100"), false, () =>
                    {
                        Av3AdvancedSettings.AddBlendshape(meshRenderer, arrayIndex, true, 0, 100);
                    });

                    __result.AddItem(new GUIContent("Av3Creator New Toggle/Animated Blendshape/100 -> 0"), false, () =>
                    {
                        Av3AdvancedSettings.AddBlendshape(meshRenderer, arrayIndex, true, 100, 0);
                    });


                    if (instance != null && instance.AdvancedToggles != null && instance.AdvancedToggles.Count > 0)
                    {
                        foreach (var advancedToggle in instance.AdvancedToggles)
                        {
                            string name = string.IsNullOrEmpty(advancedToggle.Name) ? "Toggle " + (Array.IndexOf(instance.AdvancedToggles.ToArray(), advancedToggle) + 1) : advancedToggle.Name;

                            __result.AddItem(new GUIContent("Av3Creator Existing Toggle/" + name + "/Custom Blendshape"), false, () =>
                            {
                                var window = EditorWindow.GetWindow<Av3BlendshapeCreator>(utility: true, title: "Blendshape Creator: " + blendShapeName, focus: true);
                                window.minSize = new Vector2(300, 100);
                                window.maxSize = window.minSize + new Vector2(100, 0);
                                window.SetParameter(meshRenderer, new Av3Blendshape(arrayIndex, blendShapeName, 0, 100), advancedToggle);
                                window.ShowPopup();
                            });

                            __result.AddItem(new GUIContent("Av3Creator Existing Toggle/" + name + "/Blendshape/0 -> 100"), false, () =>
                                {
                                    advancedToggle.AddBlendshape(meshRenderer, arrayIndex, false, 0, 100);
                                });

                            __result.AddItem(new GUIContent("Av3Creator Existing Toggle/" + name + "/Blendshape/100 -> 0"), false, () =>
                            {
                                advancedToggle.AddBlendshape(meshRenderer, arrayIndex, false, 100, 0);
                            });

                            __result.AddItem(new GUIContent("Av3Creator Existing Toggle/" + name + "/Animated Blendshape/0 -> 100"), false, () =>
                            {
                                advancedToggle.AddBlendshape(meshRenderer, arrayIndex, true, 0, 100);
                            });

                            __result.AddItem(new GUIContent("Av3Creator Existing Toggle/" + name + "/Animated Blendshape/100 -> 0"), false, () =>
                            {
                                advancedToggle.AddBlendshape(meshRenderer, arrayIndex, true, 100, 0);
                            });
                        }
                    }

                }
                else if (arrayIndex >= 0 && targetObject != null && targetObject is Renderer && targetName == "m_Materials")
                {
                    var renderer = targetObject as Renderer;
                    var selectedMaterial = renderer.sharedMaterials[arrayIndex];

                    __result.AddDisabledItem(new GUIContent("Selected: " + selectedMaterial.name));
                    __result.AddItem(new GUIContent("Av3Creator Advanced Toggle/Create New Toggle"), false, () =>
                    {
                        var window = EditorWindow.GetWindow<Av3MaterialSwapCreator>(utility: true, title: "Av3Creator - Material Swap", focus: true);
                        window.minSize = new Vector2(300, 80);
                        window.maxSize = window.minSize + new Vector2(100, 0);
                        window.SetParameter(renderer, selectedMaterial, arrayIndex);
                        window.ShowPopup();
                    });

                    var instance = Av3AdvancedSettings.Instance;
                    if (instance != null && instance.AdvancedToggles != null && instance.AdvancedToggles.Count > 0)
                    {
                        foreach (var advancedToggle in instance.AdvancedToggles)
                        {
                            string name = string.IsNullOrEmpty(advancedToggle.Name) ? "Toggle " + (Array.IndexOf(instance.AdvancedToggles.ToArray(), advancedToggle) + 1) : advancedToggle.Name;
                            //if (string.IsNullOrEmpty(advancedToggle.Name)) continue;
                            __result.AddItem(new GUIContent("Av3Creator Advanced Toggle/Add to Existing/" + name + ""), false, () =>
                            {
                                var window = EditorWindow.GetWindow<Av3MaterialSwapCreator>(utility: true, title: "Av3Creator - Material Swap", focus: true);
                                window.minSize = new Vector2(300, 80);
                                window.maxSize = window.minSize + new Vector2(100, 0);
                                window.SetParameter(renderer, selectedMaterial, arrayIndex, advancedToggle);
                                window.ShowPopup();
                            });
                        }
                    }

                }
            } catch(Exception error)
            {
                Debug.LogException(error);
            }
        }

        private static bool Av3Patch(ref bool __result, string filename)
        {
            // my script isnt needed in the package of avatars
            // this will prevent this
            if (filename.Contains("Av3Creator/Editor") || filename.Contains("Av3Creator/Dependencies"))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}