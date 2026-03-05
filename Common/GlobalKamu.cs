
namespace LatokoneAI.Common
{
    public static class GlobalKamu
    {
        public static string RegistryRoot { get { return "Software\\KamuAI\\"; } }


        public static string ExePath
        {
            get
            {
                return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
        }
    }
}
