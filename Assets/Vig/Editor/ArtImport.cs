// Import settings for the Blender-built models under Assets/Vig/Art (DECISIONS U13): meshes only, at the
// scale and facing the build scripts wrote them (art/blender_kit.py), with their own smooth normals. No
// materials (People.cs makes them by role), no animation, cameras, lights or rig: the parts are rigid
// and posed by code.
using UnityEditor;

namespace Vig.EditorTools
{
    public sealed class ArtImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Vig/Art/")) return;
            var m = (ModelImporter)assetImporter;
            m.globalScale = 1;
            m.useFileScale = true;
            m.bakeAxisConversion = false;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.importAnimation = false;
            m.animationType = ModelImporterAnimationType.None;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = false;
            m.importVisibility = false;
            m.importNormals = ModelImporterNormals.Import;
            m.importTangents = ModelImporterTangents.None;
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.isReadable = false;
            m.optimizeMeshPolygons = true;
            m.optimizeMeshVertices = true;
            m.weldVertices = true;
            m.preserveHierarchy = true;
            m.sortHierarchyByName = false;
        }
    }
}
