// compile me: mcs Patches/SVLoader.cs
using System;
using System.Collections.Generic;
using System.Security.Policy;
using System.IO;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;

public class MMLoader {
    private class Entry_t {
        public string Name;
        public Assembly Loaded;

        public Entry_t(string Name)
        {
            this.Name = Name;
            this.Loaded = null;
        }
    };

    private static Dictionary<string, Entry_t> translateAssemblyName = new Dictionary<string, Entry_t>{
        {"Microsoft.Xna.Framework", new Entry_t("MonoGame.Framework")},
        {"Microsoft.Xna.Framework.Game", new Entry_t("MonoGame.Framework")},
        {"Microsoft.Xna.Framework.Graphics", new Entry_t("MonoGame.Framework")},
        {"Microsoft.Xna.Framework.Xact", new Entry_t("MonoGame.Framework")},
        {"StardewValley", new Entry_t("Stardew Valley")}
    };
    
    private static Assembly ResolveXNAAssemblies(object sender, ResolveEventArgs args)
    {
        string smallAssemblyName = args.Name.Split(new char[] { ',' })[0];

        Entry_t entry = null;
        translateAssemblyName.TryGetValue(smallAssemblyName, out entry);

        if (entry == null)
            return null;

        if (entry.Loaded == null)
            entry.Loaded = Assembly.Load(entry.Name);
        if (entry.Loaded == null)
            entry.Loaded = Assembly.Load(smallAssemblyName);

        return entry.Loaded;
    }

    public static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("SVLoader: Runtime relinker for Stardew Valley on Linux.");
            Console.Error.WriteLine("Syntax: SVLoader.exe <assembly> [appBasePath] [-- appArgs...]");
            Console.Error.WriteLine("     asssembly: Assembly to be loaded.");
            Console.Error.WriteLine("   appBasePath: Optionally Override AppDomain.CurrentDomain.BaseDirectory.");
            Console.Error.WriteLine("          '--': Anything afterwards is passed as arguments to the Assembly.");
            System.Environment.Exit(-1);
        }

        if (!File.Exists(args[0]))
        {
            Console.Error.WriteLine($"Unable to find assembly \"{args[0]}\"!");
            System.Environment.Exit(-1);
        }

        // Gather Arguments
        string assemblyFilePath = Path.GetDirectoryName(Path.GetFullPath(args[0]));
        Assembly assembly = Assembly.ReflectionOnlyLoadFrom(args[0]);
        AssemblyName assemblyName = assembly.GetName();
        string appBasePath = assemblyFilePath;
        string[] appArgs = new string[] { }; 
        try
        {
            int i = 1;
            if ((string)args.GetValue(i)?.ToString() != "--")
                appBasePath = (string)args?.GetValue(i++);

            if ((string)args?.GetValue(i) == "--")
                appArgs = args?.Skip(i+1).ToArray();
        }
        catch (Exception) { }

        // Create domain and load the base assembly
        Evidence evidence = new Evidence(AppDomain.CurrentDomain.Evidence);
        AppDomain domain = AppDomain.CreateDomain(assemblyName.Name, evidence, appBasePath, appBasePath, false);       

        // Resolve bad XNA references to MonoGame
        domain.AssemblyResolve += new ResolveEventHandler(ResolveXNAAssemblies);

        // Change current directory and jump into the assembly
        Directory.SetCurrentDirectory(appBasePath);
        domain.ExecuteAssembly(args[0], appArgs);
    }
}
