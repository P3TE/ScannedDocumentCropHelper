using System.IO;

namespace DefaultNamespace
{
    public class InputOutputImageFilePair
    {
        public string InputFilePath { get; private set; }
        public string OutputFilePath { get; private set; }
        
        public bool OutputFileExists { get; private set; }

        public InputOutputImageFilePair(string inputFilePath, string outputFilePath)
        {
            this.InputFilePath = inputFilePath;
            this.OutputFilePath = outputFilePath;
            this.OutputFileExists = File.Exists(outputFilePath);
        }

        public void OnOutputFileCreated()
        {
            OutputFileExists = true;
        }
    }
}