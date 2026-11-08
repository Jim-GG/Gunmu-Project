using System.Threading.Tasks;
public static class BuildCrystalParkourDemo
{
    public static async Task<object> Run()=>await CrystalParkour.Editor.CrystalParkourBuilder.Build();
}
