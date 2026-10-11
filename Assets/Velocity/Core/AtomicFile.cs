using System;
using System.IO;
using System.Text;

namespace Velocity.Core
{
    public static class AtomicFile
    {
        public static void Write(string path,string text)
        {
            string temporary=path+".tmp";
            using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(stream,new UTF8Encoding(false)))
            {writer.Write(text);writer.Flush();stream.Flush(true);}
            if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);
        }
        public static string Read(string path,Func<string,bool> valid)
        {
            foreach(string candidate in new[]{path,path+".bak"})
            {
                try {if(File.Exists(candidate)){string text=File.ReadAllText(candidate);if(valid(text))return text;}}
                catch(IOException){ }
            }
            return null;
        }
    }
}
