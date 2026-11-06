using System.Threading.Tasks;
public static class PrepareActualModernLabModules
{
 public static async Task<object> Run()=>await ModernLab.Editor.ModernLabMaterialSetup.Setup();
}
