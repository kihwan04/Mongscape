using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Muhanok.Editor
{
    public static class MuhanokGameViewFit
    {
        private const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        [MenuItem("Mongscape/Fit Game View (16:9)")]
        public static void Fit()
        {
            try
            {
                var assembly=typeof(EditorWindow).Assembly;
                var type=assembly.GetType("UnityEditor.GameView"); var window=EditorWindow.GetWindow(type);
                var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
                var sizes=sizesType.BaseType.GetProperty("instance",BindingFlags.Static|BindingFlags.Public).GetValue(null);
                var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");
                var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});
                var gt=group.GetType(); int builtin=(int)gt.GetMethod("GetBuiltinCount").Invoke(group,null);
                int custom=(int)gt.GetMethod("GetCustomCount").Invoke(group,null), selected=-1;
                for(int i=0;i<builtin+custom;i++)
                {
                    var size=gt.GetMethod("GetGameViewSize").Invoke(group,new object[]{i}); var st=size.GetType();
                    if((int)st.GetProperty("width").GetValue(size)==1280&&(int)st.GetProperty("height").GetValue(size)==720) { selected=i; break; }
                }
                if(selected<0)
                {
                    var sizeType=assembly.GetType("UnityEditor.GameViewSize"); var mode=assembly.GetType("UnityEditor.GameViewSizeType");
                    var size=Activator.CreateInstance(sizeType,Instance,null,new object[]{Enum.Parse(mode,"FixedResolution"),1280,720,"Muhanok 16:9"},null);
                    gt.GetMethod("AddCustomSize").Invoke(group,new[]{size}); selected=builtin+custom;
                }
                type.GetMethod("SizeSelectionCallback",Instance).Invoke(window,new object[]{selected,null});
                type.GetMethod("ConfigureZoomArea",Instance).Invoke(window,null);
                var target=(Vector2)type.GetProperty("targetRenderSize",Instance).GetValue(window);
                var view=(Rect)type.GetProperty("viewInWindow",Instance).GetValue(window);
                float scale=(float)type.GetMethod("DefaultScaleForTargetInView",Instance).Invoke(window,new object[]{target,view.size});
                var zoom=type.GetField("m_ZoomArea",Instance).GetValue(window);
                zoom.GetType().GetMethod("SetTransform",Instance).Invoke(zoom,new object[]{Vector2.zero,Vector2.one*scale});
                type.GetField("m_defaultScale",Instance).SetValue(window,scale);
                type.GetMethod("EnforceZoomAreaConstraints",Instance).Invoke(window,null); window.Focus(); window.Repaint();
                Debug.Log("MUHANOK_GAME_VIEW_FIT: 1280x720 / scale "+scale);
            }
            catch(Exception error) { Debug.LogWarning("Game view fit unavailable: "+error.GetBaseException().Message); }
        }
    }
}
