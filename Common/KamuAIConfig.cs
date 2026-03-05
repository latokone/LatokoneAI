namespace LatokoneAI.Common
{
    public class LlmConfig
    {
        public int SelectedModel { get; set; }
        public int SelectedAccelerator { get; set; }

        public List<LlmModel> Models { get; set; }
        public List<LlmAccelerator> Accelerators { get; set; }

        public string[] SystemRoles { get; set; }

        public string[][] ChatMessages { get; set; }

        public List<string[]> AntiPromptLists { get; set; }

        static string modelPath = "";
        public static string ModelPath
        {
            get
            {
                //var defaultDir = Path.Combine(AppContext.BaseDirectory, "Models", "LLM");
                //return RegistryEx.Read("ModelPathLLM", defaultDir, "Models");
                return modelPath;
            }
            set
            {
                //RegistryEx.Write("ModelPath", value, "ReBuzzChat");
                modelPath = value;
            }
        }

        public int SelectedLanguage { get; set; }
    }

    public class LlmModel
    {
        public string Name { get; set; }
        public string Filename { get; set; }
        public string Url { get; set; }

        public override string ToString() { return Name; }
    }

    public class LlmAccelerator
    {
        public string Name { get; set; }
        public string Library { get; set; }

        public override string ToString() { return Name; }
    }

}
