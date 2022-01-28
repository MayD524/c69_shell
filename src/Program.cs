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
        static List<bool> logicChain = new List<bool>() { true };
        static int filePos = 0;

        static void Main(string[] args)
        {
            if (env == null)
            {
                // check if the env file exists
                env = new shellEnv(Exists("./env/env.conf"));
            }
            
            Console.WriteLine("Welcome to the C#69 Shell!\nAuthors: May & Sweden");

            if (env.getVar("HOME").value == "")
                env.setEnv("HOME", Directory.GetCurrentDirectory());
            
            if (env.getVar("LOAD_SETUP_SCRIPT").value == "1")
            
                // load the startup file
                scriptHandler("./startup.c69");
            

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
            int lastFilePos = filePos;
            List<string> lastScript = currentFile;
            filePos = 0;
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
                Console.WriteLine(scriptFile + "<" + filePos + "> :: " + currentFile[filePos]);
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
            filePos = lastFilePos;
            currentFile = lastScript;
        }

        static void taskHandler(List<string> task){
            // check the end of logicChain
            if (logicChain.Count > 0 && !logicChain[logicChain.Count - 1])
            {
                if (task.Contains("end"))
                {
                    logicChain.RemoveAt(logicChain.Count - 1);
                    return;
                }
                else if (task.Contains("else"))
                {
                    logicChain[logicChain.Count - 1] = true;
                    return;
                }
                else if (task.Contains("elif"))
                {
                    task[task.IndexOf("elif")] = "if";
                    logicChain.RemoveAt(logicChain.Count - 1);
                }
                else
                    return;
            }
            // loop through the task
            List<envVar> buffer = new List<envVar>();

            if (task[0] == "") { return; }

            for(int i = 0; i < task.Count; i++)
            {                
                
                if (task[0] != "set-alias" && task[i].StartsWith("$") && env.varExists(task[i].Substring(1)))
                { 
                    task[i] = env.getVar(task[i].Substring(1)).value;
                }
                else if ( (task[0] != "set-alias" || i > 1) && task[0] != "func" &&  env.aliasExists(task[i]))
                {
                    // set the alias
                    task[i] = env.getAlias(task[i]);
                    if (task[i].Contains(" ")) {
                        // split the alias into a list
                        List<string> alias = split(task[i], ' ');
                        // insert the alias into the task
                        task.InsertRange(i + 1, alias);
                        // remove the alias from the task
                        task.RemoveAt(i);

                    }
                }
                else if (task[0] != "set-alias" && env.funcExists(task[i]) && task[0] != "call" && task[0] != "func")
                {
                    // add call to task[0]
                    task.Insert(0, "call");
                }
            }

            switch (task[0].ToLower()) {
                case "}": break;
                case "func":
                    envFunction func = new envFunction();
                    List<string> funcArgs = new List<string>();
                    func.name = task[1];
                    if (env.funcExists(func.name))
                        env.removeFunction(func.name);
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

                case "elif": break;
                case "else": 
                    logicChain[logicChain.Count - 1] = false;
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
                            if (task.Count - 2 < f.numArgs)
                                throw new Exception("Not enough arguments given");
                            else if (task.Count - 2 == f.numArgs) {
                                for (int i = 2; i < task.Count; i++)
                                {
                                    fargs.Add(task[i]);
                                }
                            }
                            for (int i = 2; i < task.Count; i++)
                            {
                                if (task.Count - 2 > f.numArgs && i - 2 < f.numArgs)
                                    fargs.Add(task[i]);
                                else {
                                    fargs.Add(String.Join(" ", task.GetRange(i, task.Count - i)));
                                    break;
                                }
                            }
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
                        env.setEnv("lastTaskExitCode", "null");
                        // run the function
                        foreach (string line in f.code)
                        {
                            taskHandler(split(line, ' '));
                            if (env.getVar("lastTaskExitCode").value != "null")
                                break;
                        }
                        if (env.getVar("lastTaskExitCode").value != "0" || env.getVar("lastTaskExitCode").value != "null")
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

                case "check-is":
                    if (task.Count < 3)
                        throw new Exception("No variable name given");
                    envVar v = new envVar();
                    v.name = task[1];

                    switch(task[1]){
                        case "null":
                            v.value = task[2] == "NULL" ? "1" : "0";
                            break;

                        case "empty":
                            v.value = task[2] == "" ? "1" : "0";
                            break;

                        case "equal":
                            v.value = task[2] == task[3] ? "1" : "0";
                            break;

                        case "notequal":
                            v.value = task[2] != task[3] ? "1" : "0";
                            break;

                        case "greater":
                            v.value = Convert.ToInt32(task[2]) > Convert.ToInt32(task[3]) ? "1" : "0";
                            break;

                        case "less":
                            v.value = Convert.ToInt32(task[2]) < Convert.ToInt32(task[3]) ? "1" : "0";
                            break;

                        case "greater-equal":
                            v.value = Convert.ToInt32(task[2]) >= Convert.ToInt32(task[3]) ? "1" : "0";
                            break;

                        case "less-equal":
                            v.value = Convert.ToInt32(task[2]) <= Convert.ToInt32(task[3]) ? "1" : "0";
                            break;

                        case "in":
                            v.value = task[2].Contains(task[3]) ? "1" : "0";
                            break;
                    }
                    // check if '->' is used
                    if (task.Contains("->"))
                    {
                        buffer.Add(v);
                        break;
                    }
                    Console.WriteLine(v.value);
                    break;

                case "set-alias":
                    if (task.Count < 3)
                        throw new Exception("No alias name given");
                    if (env.aliasExists(task[1]))
                        env.removeAlias(task[1]);
                    env.addAlias(task[1], String.Join(" ", task.GetRange(2, task.Count - 2)));
                    break;

                case "user-input":
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

                case "file-read":
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
                case "rename":
                    if(task[1].Contains("/")){
                        if(task[2].Contains("/")){
                            File.Copy(task[1],task[2]);
                            File.Delete(task[1]);
                        }else{
                            
                            goto stderr;
                        }
                    }else{    
                        File.Copy(env.getVar("PWD").value+ "\\" + task[1],env.getVar("PWD").value+ "\\" + task[2]);
                        File.Delete(env.getVar("PWD").value+ "\\" + task[1]);
                    }
                    break;
                    
                case "delete":
                    if (task.Count <= 1)
                        throw new Exception("delete: missing file name");
                    else if(task[1].Contains("env.conf"))
                        goto stderr;
                    else{
                        File.Delete(task[1]);

                    }
                    break;

                case "copy":
                    if(task[1].Contains("\\"))
                        File.Copy(task[1],task[2]);
                    else
                        File.Copy(env.getVar("PWD").value+ "\\" + task[1],task[2]);
                    break;

                case "move":
                    if (task.Count < 3)
                        throw new Exception("move: missing file name");
                    if (fileExists(task[1]))
                    {
                        if (task[2].Contains("\\"))
                            File.Move(task[1], task[2]);
                        else
                            File.Move(env.getVar("PWD").value + "\\" + task[1], env.getVar("PWD").value + "\\" + task[2]);
                    }
                    else
                        throw new Exception("move: file does not exist");
                    break;
                    
                case "make-direcory":
                    if (task.Count <= 1)
                        throw new Exception("make-direcory: missing directory name");
                    Directory.CreateDirectory(task[1]);
                    break;
                
                case "make-file":
                    if (task.Count <= 1)
                        throw new Exception("make-file: missing file name");
                    using (StreamWriter sw = File.CreateText(task[1]))
                    {
                        sw.Write("");
                    }
                    break;

                case "file-write":
                    if (task.Count <= 1)
                        throw new Exception("file-write: missing file name");
                    if (task.Count <= 2)
                        throw new Exception("file-write: missing content");
                    using (StreamWriter sw = File.AppendText(task[1]))
                    {
                        sw.WriteLine(task[2]);
                    }
                    break;

                case "if":
                    // start an if statement
                    if (task.Count < 2)
                        throw new Exception("if: missing condition");
                    
                    if (task[1].Contains("!"))
                        logicChain.Add(task[1].Substring(1) == "0" ? true : false);
                    else
                        logicChain.Add(task[1] == "0" ? false : true);
                    
                    break;

                case "end":
                    if (logicChain.Count == 1)
                        throw new Exception("end: no if statement found");
                    logicChain.RemoveAt(logicChain.Count - 1);
                    break;

                case "item-exists":
                    if (task.Count < 4)
                        throw new Exception("item-exists: missing item name");
                    switch (task[1]) {
                        case "file":
                            env.setEnv(task[3], (File.Exists(task[2]) ? "1" : "0"));
                            break;

                        case "dir":
                            env.setEnv(task[3], (Directory.Exists(task[2]) ? "1" : "0"));
                            break; 
                        case "var":
                            env.setEnv(task[3], (env.varExists(task[2]) ? "1" : "0"));
                            break;
                        case "func":
                            env.setEnv(task[3], env.funcExists(task[2]) ? "1" : "0");
                            break;
                        default:
                            throw new Exception("item-exists: unknown item type");
                    }
                    break;
                
                case "exit-func":
                    env.setEnv("lastTaskExitCode", task[1]);
                    break;

                case "set-pwd":
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

                case "list-contents":
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
                case "no":
                    Console.WriteLine("No");
                    goto stderr;

                case "std-error":
                    stderr:
                        Console.WriteLine("char(69)");
                        throw new Exception("you're stupid -_-");

                case "print-console":
                    if (task.Count >= 2)
                    {
                        string output = "";
                        // join the rest of the arguments
                        for (int i = 1; i < task.Count; i++)
                        {
                            if (task[i] == "->") { break;}
                            output += task[i] + " ";
                        }
                        Console.Write(enableEscapeChars(output));
                        buffer.Add(new envVar(){ name = "print-console", value = output, type = (int)types.stringType, isReadOnly = true});
                    }
                    else
                        throw new Exception("print-console: too few arguments");
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
