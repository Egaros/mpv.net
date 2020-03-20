﻿
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;

namespace ScriptHost
{
    public class PowerShell
    {
        public Runspace Runspace { get; set; }
        public Pipeline Pipeline { get; set; }
        public string Module { get; set; }
        public bool Print { get; set; }
        public List<string> Scripts { get; } = new List<string>();
        public string[] Parameters { get; }

        public static List<PowerShell> Instances { get; } = new List<PowerShell>();

        string BR = Environment.NewLine;

        public object Invoke()
        {
            try
            {
                Runspace = RunspaceFactory.CreateRunspace();
                Runspace.ApartmentState = ApartmentState.STA;
                Runspace.Open();
                Pipeline = Runspace.CreatePipeline();

                foreach (string script in Scripts)
                    Pipeline.Commands.AddScript(script);

                if (Parameters != null)
                    foreach (string param in Parameters)
                        foreach (Command command in Pipeline.Commands)
                            command.Parameters.Add(null, param);

                Runspace.SessionStateProxy.SetVariable("ScriptHost", this);

                if (Print)
                {
                    Pipeline.Output.DataReady += Output_DataReady;
                    Pipeline.Error.DataReady += Error_DataReady;
                }
            
                return Pipeline.Invoke();
            }
            catch (RuntimeException e)
            {
                string message = e.Message + BR + BR + e.ErrorRecord.ScriptStackTrace.Replace(
                    " <ScriptBlock>, <No file>", "") + BR + BR + Module + BR;

                throw new PowerShellException(message);
            }
            catch (Exception e)
            {
                throw e;
            }        
        }

        public void Output_DataReady(object sender, EventArgs e)
        {
            var output = sender as PipelineReader<PSObject>;

            while (output.Count > 0)
                ConsoleHelp.Write(output.Read().ToString(), Module);
        }

        public void Error_DataReady(object sender, EventArgs e)
        {
            var output = sender as PipelineReader<Object>;

            while (output.Count > 0)
                ConsoleHelp.WriteError(output.Read().ToString(), Module);
        }

        public void RedirectEventJobStreams(PSEventJob job)
        {
            if (Print)
            {
                job.Output.DataAdded += Output_DataAdded;
                job.Error.DataAdded += Error_DataAdded;
            }
        }

        void Output_DataAdded(object sender, DataAddedEventArgs e)
        {
            var output = sender as PSDataCollection<PSObject>;
            ConsoleHelp.Write(output[e.Index], Module);
        }

        void Error_DataAdded(object sender, DataAddedEventArgs e)
        {
            var error = sender as PSDataCollection<ErrorRecord>;
            ConsoleHelp.WriteError(error[e.Index], Module);
        }
    }

    public class PowerShellException : Exception
    {
        public PowerShellException(string message) : base(message)
        {
        }
    }
}
