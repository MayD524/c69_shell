using static c69_shellTools.c69_shellTools;
using System.Collections.Generic;
using c69_shellEnv;
using System.IO;
using System;

namespace c69_shell
{
    class Program
    {
        static bool isAlive = true;
        static shellEnv env = null;
        static List<string> currentFile = new List<string>();
        static int filePos = 0;

        static void Main(string[] args)
        {
            if (env == null)
            {
                // check if the env file exists
                env = new shellEnv(Exists("./env/env.conf"));
            }
            
            Console.WriteLine("Welcome to the C#69 Shell!\nAuthors: May & Sweden");
            if (env.getVar("USER").value == "")
            {
                Console.Write("Please enter your username: ");
                env.setEnv("USER", Console.ReadLine());
            }

            if (env.getVar("HOME").value == "")
                env.setEnv("HOME", Directory.GetCurrentDirectory());
            


            // this should be in a while loop
            while(isAlive){
                Console.Write(env.getVar("PROMPT").value);
                env.setEnv("PWD", Directory.GetCurrentDirectory());
                string input = Console.ReadLine();
                input = split(input, '#')[0].Trim(); // remove comments

                List<string> tasks = split(input, ';');
                foreach (string task in tasks)
                {
                    try{
                        taskHandler(split(task.Trim(),' '));
                        env.setEnv("taskCount", (int.Parse(env.getVar("taskCount").value) + 1).ToString());
                        env.taskHistory.Add(task.Trim());
                        env.setEnv("lastTaskExitCode", "0");
                        
                    } catch(Exception e){
                        Console.WriteLine(e.Message);
                        env.setEnv("lastTaskExitCode", "1");
                    }
                }
            }
        }
        
        static void scriptHandler(string scriptFile)
        {
            // check if the file exists
            if ( !fileExists(scriptFile) )
                throw new Exception(String.Format("File {0} does not exist", scriptFile));

            // read the file
            currentFile = readFile(scriptFile);
            // remove comments
            for (int i = 0; i < currentFile.Count; i++)
            {
                currentFile[i] = split(currentFile[i], '#')[0].Trim();
            }
            while (filePos < currentFile.Count)
            {
                try
                {
                    taskHandler(split(currentFile[filePos], ' '));
                    env.setEnv("taskCount", (int.Parse(env.getVar("taskCount").value) + 1).ToString());
                    env.taskHistory.Add(currentFile[filePos]);
                    env.setEnv("lastTaskExitCode", "0");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    env.setEnv("lastTaskExitCode", "1");
                }
                filePos++;
            }
        }

        static void taskHandler(List<string> task){
            // loop through the task
            List<envVar> buffer = new List<envVar>();

            if (task[0] == "") { return; }

            for(int i = 0; i < task.Count; i++)
            {
                if (task[i].StartsWith("$") && env.varExists(task[i].Substring(1)))
                    task[i] = env.getVar(task[i].Substring(1)).value;
            }

            switch (task[0].ToLower()) {
                case "func":
                    envFunction func = new envFunction();
                    List<string> funcArgs = new List<string>();
                    func.name = task[1];
                    // see how many things till {
                    int numArgs = 0;
                    for (int i = 2; i < task.Count; i++)
                    {
                        if (task[i] == "{")
                        {
                            numArgs = i - 2;
                            break;
                        }
                        funcArgs.Add(task[i]);
                    }
                    func.argNames = funcArgs;
                    func.numArgs = numArgs;
                    // get the lines between { and }
                    List<string> funcLines = new List<string>();
                    // get the lines between { and }
                    filePos++;
                    for (int i = filePos; i < currentFile.Count; i++)
                    {
                        if (currentFile[i].Trim() == "}")
                        {
                            filePos = i;
                            break;
                        }
                        funcLines.Add(currentFile[i]);
                    }

                    func.code = funcLines;
                    env.addFunction(func.name, func);           
                    break;

                case "call":
                    // call a function
                    if (task.Count < 2)
                        throw new Exception("No function name given");
                    if (env.funcExists(task[1]))
                    {
                        // get the function
                        envFunction f = env.getFunction(task[1]);
                        // get the arguments
                        List<string> fargs = new List<string>();
                        if(f.numArgs > 0)
                        {
                            for (int i = 2; i < task.Count; i++)
                                fargs.Add(task[i]);
                            
                        }
                        // check if the number of arguments is correct
                        if (fargs.Count != f.numArgs)
                            throw new Exception(String.Format("Function {0} requires {1} arguments but got {2}", f.name, f.numArgs, fargs.Count));

                        // set the arguments
                        if (f.numArgs > 0)
                        {
                            for (int i = 0; i < f.numArgs; i++)
                                env.setEnv(f.argNames[i], fargs[i]);
                            
                        }
                        // run the function
                        foreach (string line in f.code)
                            taskHandler(split(line, ' '));
                        
                        if (env.getVar("lastTaskExitCode").value != "0")
                            throw new Exception(String.Format("Function {0} exited with code {1}", f.name, env.getVar("lastTaskExitCode").value));
                        
                        if (f.numArgs == 0)
                            break;
                        
                        // reset the arguments
                        foreach (string arg in f.argNames)
                            env.removeVar(arg);
                        
                    }
                    break;
                
                case "exec":
                    string args = "";
                    if (task.Count > 2)
                    {
                        for (int i = 2; i < task.Count; i++)
                            args += task[i] + " ";
                    }

                    System.Diagnostics.Process.Start(task[1], args);
                    break;

                case "remfunc":
                    if (task.Count < 2)
                        throw new Exception("No function name given");
                    if (env.funcExists(task[1]))
                        env.removeFunction(task[1]);
                    
                    break;

                case "input":
                    
                    string input = Console.ReadLine();
                    if (task.Count == 1)
                        Console.WriteLine(input);
                    else if (task.Count > 1)
                        env.setEnv(task[1], input);
                    
                    break;


                case "load":
                    if (task.Count <= 1)
                        throw new Exception("load: missing file name");
                    scriptHandler(task[1]);
                    break;

                case "rem":
                    if (task.Count <= 1)
                        throw new Exception("rem: missing variable name");
                    env.removeVar(task[1]);
                    break;

                case "env":
                    if (task.Count > 1)
                    {
                        switch (task[1])
                        {
                            case "out":
                                env.writeEnvFile(env.getVar("ENV_FILE").value);
                                break;
                            case "read":
                                env.readEnvFile(env.getVar("ENV_FILE").value);
                                break;
                            case "func":
                                // dispaly all functions
                                foreach (string funcName in env.getFuncNames()){
                                    envFunction f = env.getFunction(funcName);
                                    Console.WriteLine("{0}({1})", f.name, String.Join(", ", f.argNames));
                                    Console.WriteLine("{");
                                    foreach (string line in f.code)
                                        Console.WriteLine("\t" + line);
                                    Console.WriteLine("}");
                                }
                                break;
                        }
                        break;
                    }
                    env.listEnv();
                    break;

                case "read":
                    List<string> lines = readFile(task[1]);
                    if (!task.Contains("->"))
                    {
                        foreach (string line in lines)
                            Console.WriteLine(line);
                        
                        break;
                    }
                    string varName = task[2];
                    string slines = "";
                    foreach (string line in lines)
                        slines += line + "\n";
                    
                    buffer.Add(new envVar() { name = varName, value = slines, type = (int)types.stringType, isReadOnly = false });
                    break;

                case "cd":
                    if (task.Count > 1)
                    {
                        if (task[1] == "..")
                            Directory.SetCurrentDirectory(Directory.GetParent(Directory.GetCurrentDirectory()).FullName);
                        
                        else
                            Directory.SetCurrentDirectory(task[1]);
                    } 
                    else
                        Directory.SetCurrentDirectory(env.getVar("HOME").value);
                    break;

                case "type":
                    // get the type of var
                    break;

                case "cat":
                    
                    break;

                case "ls":
                    // get a list of files and folders in the current directory
                    if (task.Count > 1)
                        lsCmd(task[1]);
                    
                    else
                        lsCmd("");
                    
                    break;

                case "set":
                    if (task.Count == 3)
                    {
                        env.setEnv(task[1], task[2]);
                        break;
                    }
                    else
                    {
                        bool isReadOnly = false;
                        bool isValid = false;
                        string value = "";
                        for (int i = 2; i < task.Count; i++)
                        {
                            isValid = bool.TryParse(task[i], out isReadOnly);
                            if (isValid) { break; }
                            value += task[i] + " ";
                        }
                        env.setEnv(task[1], value, isReadOnly);
                        break;
                    }

                case "echo":
                    if (task.Count >= 2)
                    {
                        string output = "";
                        // join the rest of the arguments
                        for (int i = 1; i < task.Count; i++)
                        {
                            if (task[i] == "->") { break;}
                            output += task[i] + " ";
                        }
                        Console.WriteLine(output);
                        buffer.Add(new envVar(){ name = "echo", value = output, type = (int)types.stringType, isReadOnly = true});
                    }
                    else
                        throw new Exception("echo: too few arguments");
                    break;

                case "exit":
                    isAlive = false;
                    break;

                case "clear":
                    Console.Clear();
                    break;

                case "find":
                    break;
                
                case "history":
                    int getIndex = 0;
                    if (task.Count > 1)    
                    {
                        if (task[1].GetType() == typeof(string) && task[1] == "all")
                        {
                            for (int i = 0; i < env.taskHistory.Count; i++)
                            {
                                Console.WriteLine(env.taskHistory[i]);
                            }
                            break;
                        }
                        getIndex = int.Parse(task[1]);
                    }
                    if (getIndex < env.taskHistory.Count)
                    {
                        Console.WriteLine(env.taskHistory[getIndex]);
                        buffer.Add(new envVar() { name = "task", value = env.taskHistory[getIndex], type = (int)types.stringType });
                        break;
                    }
                    Console.WriteLine("history: no such task");
                    buffer.Add(new envVar() { name = "task", value = "history: no such task", type = (int)types.stringType });
                    break;

                default:
                    // check if the task is in the env
                    if (env.varExists(task[0]))
                        buffer.Add(env.getVar(task[0]));
                    else
                        throw new Exception("Unknown command: " + task[0]);
                    break;
            }

            // check if '->' is in the task
            if (task.Contains("->"))
            {
                // get the index of '->'
                int index = task.IndexOf("->") + 1;
                
                if (index < task.Count)
                {
                    // check if there is anything in the buffer 
                    if (buffer.Count > 0)
                        // set the value of the variable
                        env.setEnv(task[index], buffer[0].value);
                    
                    else
                        // set the value of the variable
                        env.setEnv(task[index], "NULL");
                    return; 
                } else {
                    Console.WriteLine("Error: there is nothing after '->'.");
                }
                
            }
        }
    }
}
