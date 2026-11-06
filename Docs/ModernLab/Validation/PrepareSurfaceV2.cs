using System.Threading.Tasks;
public static class PrepareModernLabSurfaceV2
{
 public static async Task<object> Run()
 {
  ModernLab.Editor.ModernLabLayeredShaderBuilder.Build();
  return await ModernLab.Editor.ModernLabMaterialSetup.Setup();
 }
}
