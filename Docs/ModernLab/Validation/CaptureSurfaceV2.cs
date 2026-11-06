using System.Threading.Tasks;
public static class CaptureModernLabSurfaceV2
{
 public static async Task<object> Run()=>await ModernLab.Editor.ModernLabSurfaceInspection.Capture();
}
