using System.Threading.Tasks;
public static class PrepareModernLabPreview
{
 public static async Task<object> Run()
 {
  await ModernLab.Editor.ModernLabMaterialSetup.RebuildPreview();
  return new{prepared=true,scene="Assets/Game/Environment/ModernLab/Scenes/ModernLabMaterialPreview.unity"};
 }
}
