#region
using Av3Creator.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;
using System.Collections.Generic;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Avatars.Components;
using System.Linq;
#endregion

namespace Av3Creator.Windows
{
    public class SelectMenu : EditorWindow
    {
        //[MenuItem("Av3Creator/Select Menu", priority = 100)]
        //public static void ShowWindow()
        //{
        //    var selectMenuWindow = CreateInstance<SelectMenu>();
        //    selectMenuWindow.minSize = new Vector2(260, 340);
        //    selectMenuWindow.titleContent = new GUIContent("Select Menu");
        //    selectMenuWindow.ShowUtility();
        //}

        public event Action<VRCExpressionsMenu> OnSelectedMenu;

        private void CreateGUI()
        {
            var root = rootVisualElement;
            if (!(AssetDatabase.GUIDToAssetPath("98ba82d221e123a4689987f7389ddc25") is string uxmlPath) || string.IsNullOrEmpty(uxmlPath))
            {
                Debug.LogError("Failed to Initialize Menu Selector: Can't find SelectMenu UXML.");
                Close();
                return;
            }

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            visualTree.CloneTree(root);

            BindFunctions();
        }

        private void BindFunctions()
        {
            
            var listView = rootVisualElement.Q<ListView>("MenuList");

            var cancelButton = rootVisualElement.Q<Button>("Cancel");
            cancelButton.clicked += () => Close();

            var selectButton = rootVisualElement.Q<Button>("Select");
            selectButton.clicked += () =>
            {
                if (listView.selectedItem != null)
                {
                    OnSelectedMenu(listView.selectedItem as VRCExpressionsMenu);
                    Close();
                }
            };

            if (AvatarDescriptor == null || AvatarDescriptor.expressionsMenu == null) return;
            List<VRCExpressionsMenu> menus = new List<VRCExpressionsMenu>();

            void GetChildren(VRCExpressionsMenu menu)
            {
                if (menu == null || menus.Contains(menu)) return;
                menus.Add(menu);

                foreach(var control in menu.controls)
                {
                    if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu != null)
                    {
                        GetChildren(control.subMenu);
                    }
                }
            }
            GetChildren(AvatarDescriptor.expressionsMenu);
            menus = menus.Distinct().ToList();

            listView.itemsSource = menus;

            listView.makeItem = () => new Label();
            listView.bindItem = (e, i) => (e as Label).text = menus[i].name;


            listView.itemHeight = 16;
            listView.selectionType = SelectionType.Single;

            listView.Refresh();
        }

        private VRCAvatarDescriptor AvatarDescriptor;
        public void SetDescriptor(VRCAvatarDescriptor avatarDescriptor)
        {
            AvatarDescriptor = avatarDescriptor;
        }
    }
}
