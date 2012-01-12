using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using GoodStuff.NaturalLanguage;

//[CustomEditor(typeof(ChildCombiner))]
public class BakedChildCombiner : EditorWindow {
	
	/// Usually rendering with triangle strips is faster.
	/// However when combining objects with very low triangle counts, it can be faster to use triangles.
	/// Best is to try out which value is faster in practice.
	public bool generateTriangleStrips = true;
	
	[MenuItem ("Window/Baked Child Combiner %#m")]
    public static void Init() {
        // Get existing open window or if none, make a new one:
        EditorWindow.GetWindow(typeof(BakedChildCombiner));
    }
	
	public void OnGUI() {
		if (Selection.activeGameObject == null) {
			GUILayout.Label("Select a game object to combine");
			return;
		}
		GUILayout.Label("GameObject to combine: " + Selection.activeGameObject.name);
		generateTriangleStrips = EditorGUILayout.Toggle("Generate Triangle Strips", generateTriangleStrips);
		if (GUILayout.Button("Combine Children")) {
			CombineChildren();
		}
	}
	
	public void CombineChildren() {
		var target = Selection.activeGameObject;
		var filters = target.GetComponentsInChildren<MeshFilter>();
		var myTransform = target.transform.worldToLocalMatrix;
		var materialToMesh = new Dictionary<Material, List<MeshCombineUtility.MeshInstance>>();
		
		for (int i = 0; i < filters.Length; ++i) {
			var filter = filters[i];
			if(filter.gameObject.name.Contains("Collider")) continue; 
			var curRenderer = filters[i].renderer;
			var instance = new MeshCombineUtility.MeshInstance();
			instance.mesh = filter.sharedMesh;
			if (curRenderer != null && instance.mesh != null) {
				instance.transform = myTransform * filter.transform.localToWorldMatrix;
				
				var materials = curRenderer.sharedMaterials;
				for (int m = 0; m < materials.Length; ++m) {
					instance.subMeshIndex = System.Math.Min(m, instance.mesh.subMeshCount - 1);
	
					List<MeshCombineUtility.MeshInstance> objects = null;
					var gotList = materialToMesh.TryGetValue(materials[m], out objects);
					if (gotList) objects.Add(instance);
					else {
						objects = new List<MeshCombineUtility.MeshInstance>();
						objects.Add(instance);
						materialToMesh.Add(materials[m], objects);
					}
				}
				
//				curRenderer.enabled = true;
			}
		}
	
		var meshCount = 0;
		var combinedChildren = new List<GameObject>();
		foreach (var keyValuePair in materialToMesh) {
			var elements = keyValuePair.Value;
			var instances = elements.ToArray();
			 
			Mesh mesh = null;
			var gameObject = new GameObject("Combined mesh");
			gameObject.transform.parent = target.transform;
			gameObject.transform.localScale = Vector3.one;
			gameObject.transform.localRotation = Quaternion.identity;
			gameObject.transform.localPosition = Vector3.zero;
			gameObject.AddComponent<MeshFilter>();
			gameObject.AddComponent<MeshRenderer>();
			gameObject.renderer.material = keyValuePair.Key;
			var filter = gameObject.GetComponent<MeshFilter>();
			mesh = MeshCombineUtility.Combine(instances, generateTriangleStrips);
			filter.mesh = mesh;
			
			combinedChildren.Add(gameObject);
		
			++meshCount;
			AddMeshToAssets(target, mesh, meshCount);
		}
		
		var clone = GameObject.Instantiate(target) as GameObject;
		clone.name = target.name;
		combinedChildren.Each(c => DestroyImmediate(c));
		clone.GetComponentsInChildren<MeshRenderer>().Where(r => r != null &&
		                                                    r.gameObject.name != "Combined mesh" &&
		                                                    !r.gameObject.name.Contains("Collider") &&
		                                                    !r.gameObject.name.Contains("Collision") &&
		                                                    !r.gameObject.name.Contains("Indoors"))
			.Each(renderer => DestroyImmediate(renderer.gameObject));
		
		var destroyedCompletely = false;
		do {
			destroyedCompletely = !DestroyEmptyGameObjectsIn(clone);
		} while (!destroyedCompletely);
		
		var assetName = string.Format("Assets/Prefabs/Combined Prefabs/{0}.prefab", clone.name);
		// can't catch the folder error, so just create the folder first manually
		var prefab = EditorUtility.CreateEmptyPrefab(assetName);
		EditorUtility.ReplacePrefab(clone, prefab);
		AssetDatabase.Refresh();
		
		DestroyImmediate(clone); // the clone must be manually destroyed because it can never be empty
		
	}
	
	bool DestroyEmptyGameObjectsIn(GameObject gameObject) {
		if (gameObject == null) return false;
		
		var destroyedAnything = false;
		foreach (Transform transform in gameObject.transform) {
			destroyedAnything = destroyedAnything || DestroyEmptyGameObjectsIn(transform.gameObject);
		}
		
		var emptyGameObject = true;
		foreach (Transform transform in gameObject.transform) {
			emptyGameObject = false;
		}
		
		if (emptyGameObject &&
		    gameObject.name != "Combined mesh" &&
		    !gameObject.name.Contains("Collider") &&
		    !gameObject.name.Contains("Collision") &&
		    !gameObject.name.Contains("Indoors")) {
			destroyedAnything = true;
			DestroyImmediate(gameObject);
		}
		return destroyedAnything;
	}
	
	void AddMeshToAssets(GameObject target, Mesh mesh, int meshCount) {
		var assetName = string.Format("Assets/Models/Combined Meshes/{0}-mesh-{1}.asset", target.name, meshCount);
//		AssetDatabase.SaveAssets();
		AssetDatabase.DeleteAsset(assetName);
		AssetDatabase.SaveAssets();
		try {
			AssetDatabase.CreateAsset(mesh, assetName);
		} catch (UnityException ) {
			AssetDatabase.CreateFolder("Assets", "Models/Combined Meshes");
			AssetDatabase.CreateAsset(mesh, assetName);
		}
		AssetDatabase.SaveAssets();
		
	}
		
}
