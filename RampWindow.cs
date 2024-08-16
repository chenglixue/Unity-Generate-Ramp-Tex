using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Elysia
{
    public class RampWindow : EditorWindow
    {
        public  static RampWindow Ins;

        #region Variable
        private static readonly int    _Color_Array = Shader.PropertyToID("_ColorArray");
        private static readonly int    _Point_Array = Shader.PropertyToID("_PointArray");
        private static readonly int    _Real_Num = Shader.PropertyToID("_GradientNums");
        private static readonly string _Lerp_Mode  = "_LERP_MODE";
        private static readonly string _Gamma_Mode = "_GAMMA_MODE";

        public bool     isShow;
        public bool     autoLinkMode;
        public Material targetMaterial;
        public string   propertyName;

        public  bool  lerpMode;
        public  bool  gammaMode;
        public  int   ribbonNum;
        private bool  _isLinked;
        private int   _level;

        private Material       _previewMat;
        private Texture2D      _oldTex;
        private Texture2D      _previewTex;
        private List<Gradient> _ribbons;
        private RenderTexture  _RT;
        private int            _size;
        private Color[]        _tempColor;
        private float[]        _tempPoint;
        private int            _targetPropertyIndex;
        private List<string>   _texNames;
        #endregion

        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    // 面板中显示material
                    EditorGUILayout.ObjectField(targetMaterial, typeof(Material), false);

                    if (!autoLinkMode && targetMaterial != null)
                    {
                        _targetPropertyIndex = EditorGUILayout.Popup("Target Ramp Tex", _targetPropertyIndex,
                            _texNames.ToArray());
                        propertyName = _texNames[_targetPropertyIndex];
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Target Ramp Tex", propertyName);
                    }

                    if (GUILayout.Button(_isLinked ? "Break Link" : "Link Target Texture2D"))
                    {
                        if (_isLinked)
                        {
                            DestoryLink();
                        }
                        else
                        {
                            StarLink();
                        }
                    }
                        
                    if (GUILayout.Button("Read Config"))
                    {
                        var path = EditorUtility.OpenFilePanel("Read Config", Application.dataPath, "asset");
                        ReadConfig(path);
                    }

                    if (GUILayout.Button("Save Config"))
                    {
                        var path = EditorUtility.SaveFilePanel("Save Config", Application.dataPath, "Gradient Config", "asset");
                        SaveConfig(path);
                    }
                }
                
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUIUtility.labelWidth = 100;
                    EditorGUIUtility.fieldWidth = 50;
                    
                    lerpMode = EditorGUILayout.ToggleLeft("Mix Ramp Tex", lerpMode);
                    gammaMode = EditorGUILayout.ToggleLeft("sRGB", gammaMode);

                    // 设置key words
                    if (_previewMat != null)
                    {
                        if (lerpMode == true)
                        {
                            _previewMat.EnableKeyword(_Lerp_Mode);
                        }
                        else if(lerpMode == false)
                        {
                            _previewMat.DisableKeyword(_Lerp_Mode);
                        }

                        if (gammaMode == true)
                        {
                            _previewMat.EnableKeyword(_Gamma_Mode);
                        }
                        else if(gammaMode == false)
                        {
                            _previewMat.DisableKeyword(_Gamma_Mode);
                        }
                    }
                    
                    ribbonNum = EditorGUILayout.IntSlider("Ribbon Num", ribbonNum, 1, 8);
                    if (_ribbons.Count != ribbonNum)
                    {
                        UpdateRibbonNum();
                    }
                    
                    _level = EditorGUILayout.IntSlider("Resolution Level", _level, 0, 4);

                    // 显示Gradient Color
                    for (var i = 0; i < _ribbons.Count; ++i)
                    {
                        _ribbons[i] = EditorGUILayout.GradientField(_ribbons[i]);
                    }
                }

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUIUtility.labelWidth = 120;
                    EditorGUIUtility.fieldWidth = 50;
                    EditorGUILayout.PrefixLabel("Preview");

                    if (_RT != null && _RT.IsCreated())
                    {
                        var rect = EditorGUILayout.GetControlRect(true, 200);
                        rect.width = 200;
                        EditorGUI.DrawPreviewTexture(rect, _RT);
                    }

                    if (GUILayout.Button("Save Tex"))
                    {
                        var path = EditorUtility.SaveFilePanel("Default Save Ramp Tex", Application.dataPath, "RampTex", "TGA");
                        SaveTex(path);
                    }
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                if (_size != (int)MathF.Pow(2, 5 + _level))
                {
                    ReNewRT();
                }
                SetGradient();
            }
        }

        private void OnInspectorUpdate()
        {
            if (_RT != null)
            {
                UpdateRT();
            }
            
            if (_isLinked)
            {
                targetMaterial.SetTexture(propertyName, _RT);
            }
        }

        private void OnDisable()
        {
            isShow = false;
            RenderTexture.active = null;
            _RT.Release();
            
            DestoryLink();
            DestroyImmediate(_previewMat);
        }

        [MenuItem("CONTEXT/Material/Elysia/Generate Ramp Tex", priority = 0)]
        public static void MatShowWindow(MenuCommand menuCommand)
        {
            if (Ins == null)
            {
                Ins = GetWindow<RampWindow>();
                Ins.InitData();
                Ins.Show();
            }
            
            Ins.targetMaterial = menuCommand.context as Material;
            Ins.UpdateProperty();
        }

        public static void ShowWindow(Material material, string propName)
        {
            if (Ins == null)
            {
                Ins = GetWindow<RampWindow>();
                Ins.InitData();
                Ins.Show();
            }

            Ins.targetMaterial = material;
            Ins.propertyName = propName;
            Ins._isLinked = true;
            Ins.autoLinkMode = true;
            Ins.UpdateProperty();
        }

        private void InitData()
        {
            isShow = true;
            
            _ribbons = new List<Gradient>();
            _ribbons.Add(new Gradient());
            _texNames = new List<string>();
            
            var shader = Shader.Find("Elysia/Ramp Generator");
            _previewMat = new Material(shader);
            NewRT();
            SetGradient();
        }
        void NewRT()
        {
            ReleaseOldRT();

            _size = (int)Mathf.Pow(2, 5 + _level);
            _RT = new RenderTexture(_size, _ribbons.Count, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
            _RT.name = "previewTex";
            _RT.enableRandomWrite = true;
            _RT.Create();
        }
        void ReleaseOldRT()
        {
            if (_RT != null && _RT.IsCreated())
            {
                _RT.Release();
            }
        }
        void ReNewRT()
        {
            ReleaseOldRT();
            NewRT();
        }
        
        /// <summary>
        /// 为material传递所有gradient
        /// </summary>
        void SetGradient()
        {
            _tempColor = new Color[80];
            _tempPoint = new float[80];

            for (var i = 0; i < _ribbons.Count; ++i)
            {
                SetGradientArray(_ribbons[i], i);
            }
            
            _previewMat.SetColorArray(_Color_Array, _tempColor);
            _previewMat.SetFloatArray(_Point_Array, _tempPoint);
            _previewMat.SetFloat(_Real_Num, _ribbons.Count);
        }
        
        /// <summary>
        /// 处理每个gradient,随后传递给shader
        /// </summary>
        /// <param name="source"> 待处理的gradient </param>
        /// <param name="index"> 待处理的gradient的索引 </param>
        void SetGradientArray(Gradient source, int index)
        {
            // unity 提供的gradient，每个最多有8个keys
            // 但因为可能存在keys不在0 和 1的情况,所以设置keys的最大个数为10
            var gradientOffset = index * 10;
            var length = source.colorKeys.Length;   // 至少存在两个keys

            for (var i = 0; i < 10; ++i)
            {
                // time不在0处,且为开头时,不对它之前的color blend
                if (i == 0 && source.colorKeys[0].time != 0)
                {
                    _tempColor[gradientOffset] = source.colorKeys[0].color;
                    _tempPoint[gradientOffset] = 0;
                    continue;
                }

                if (i < length)
                {
                    _tempColor[gradientOffset + i] = source.colorKeys[i].color;
                    _tempPoint[gradientOffset + i] = source.colorKeys[i].time;
                }
                else
                {
                    // 超出部分取gradient的末尾keys
                    _tempColor[gradientOffset + i] = source.colorKeys[length - 1].color;
                    _tempPoint[gradientOffset + i] = 1;
                }
            }
        }

        /// <summary>
        /// 获得material的tex Property
        /// </summary>
        void UpdateProperty()
        {
            if (targetMaterial == null) return;

            targetMaterial.GetTexturePropertyNames(_texNames);
        }

        void UpdateRibbonNum()
        {
            // 大于手动设置的上限
            while (_ribbons.Count > ribbonNum)
            {
                _ribbons.RemoveAt(_ribbons.Count - 1);
            }

            // 小于手动设置的范围，则新增
            while (_ribbons.Count < ribbonNum)
            {
                var last = _ribbons[^1];
                
                var newGradient = new Gradient();
                newGradient.SetKeys(last.colorKeys, last.alphaKeys);
                
                _ribbons.Add(newGradient);
            }
        }

        void ReadConfig(string inPath)
        {
            if (string.IsNullOrEmpty(inPath)) return;

            var asset = AssetDatabase.LoadAssetAtPath("Assets" + inPath.Substring(Application.dataPath.Length), typeof(RampAsset)) as RampAsset;
            ribbonNum = asset.gradients.Length;
            _ribbons  = new List<Gradient>(asset.gradients.ToArray());
        }

        void SaveConfig(string inPath)
        {
            var asset = CreateInstance<RampAsset>();
            var tempGradient = new Gradient[_ribbons.Count];

            for (int i = 0; i < tempGradient.Length; ++i)
            {
                tempGradient[i] = new Gradient();
                tempGradient[i].SetKeys(_ribbons[i].colorKeys, _ribbons[i].alphaKeys);
            }

            asset.gradients = tempGradient;
            AssetDatabase.CreateAsset(asset, "Assets" + inPath.Substring(Application.dataPath.Length));
        }

        void DestoryLink()
        {
            _isLinked = false;
        }

        void StarLink()
        {
            _isLinked = true;
            _oldTex = targetMaterial.GetTexture(propertyName) as Texture2D;
        }

        void SaveTex(string inPath)
        {
            RenderTexture.active = _RT;
            _previewTex = new Texture2D(_RT.width, _ribbons.Count, TextureFormat.RGB24, false);
            _previewTex.ReadPixels(new Rect(0, 0, _RT.width, _RT.height), 0, 0);
            _previewTex.wrapMode = TextureWrapMode.Clamp;
            RenderTexture.active = null;

            var bytes = _previewTex.EncodeToTGA();
            var ts = DateTime.Now.ToString().Split(' ', ':', '/');
            var time = string.Concat(ts);
            var assetPath = "/Gradient Tex" + time + ".TGA";
            var fullPath = Application.dataPath + assetPath;
            var path = string.IsNullOrEmpty(inPath) ? fullPath : inPath;
            
            File.WriteAllBytes(path, bytes);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (_isLinked)
            {
                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                }
            
                var savedTex = AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D;
                if (savedTex != null)
                {
                    savedTex.wrapMode = TextureWrapMode.Clamp;
                    AssetDatabase.SaveAssets();
                    targetMaterial.SetTexture(propertyName, savedTex);
                }
            
                _oldTex = savedTex;
                _isLinked = false;
            }
        }

        void UpdateRT()
        {
            if (_RT.IsCreated())
            {
                Graphics.Blit(Texture2D.whiteTexture, _RT, _previewMat);
            }
        }
    }
}
