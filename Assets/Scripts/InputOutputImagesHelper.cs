using System.Collections.Generic;
using System.IO;

namespace DefaultNamespace
{
    public class InputOutputImagesHelper
    {
        public string InputDirectory { get; private set; }
        public string OutputDirectory { get; private set; }

        private List<InputOutputImageFilePair> foundImages = new();
        
        public InputOutputImagesHelper(string inputDirectory, string outputDirectory)
        {
            InputDirectory = inputDirectory;
            OutputDirectory = outputDirectory;
        }

        public void FindExistingFiles()
        {
            foreach (string inputFilePath in Directory.EnumerateFiles(InputDirectory, "*.*", SearchOption.AllDirectories))
            {
                string relativeDirectoryPath = Path.GetRelativePath(InputDirectory, inputFilePath);
                
                string expectedOutputFilePath = Path.Combine(OutputDirectory, relativeDirectoryPath);
                
                InputOutputImageFilePair filePair = new InputOutputImageFilePair(inputFilePath, expectedOutputFilePath);
                foundImages.Add(filePair);
            }
        }
        
        public int TotalFileCount => foundImages.Count;

        public int GetExistingOutputFileCount()
        {
            int count = 0;
            
            foreach (InputOutputImageFilePair filePair in foundImages)
            {
                if (filePair.OutputFileExists)
                {
                    count++;
                }
            }
            
            return count;
        }
        
        public InputOutputImageFilePair this[int index] => foundImages[index];
    }
}