﻿/*
------------------------------------------------------------------------------------------
Author:
Boroş Tiberiu                                                                                                  
Administrator Sistem                                                    Tel.: +40745310081
Institutul de Cercetări pentru Inteligenţă Artificială,
Academia Română
Calea 13 Septembrie, Nr. 13, CASA ACADEMIEI, Bucuresti 050711, ROMANIA
Tel.: +40-(0)213188103 
Fax: +40-(0)213188142 
E-mail: office@racai.ro
Web: http://www.racai.ro
------------------------------------------------------------------------------------------
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Microsoft.Win32;
using System.Threading;
using System.Globalization;

//Notes:  
//  04/27/16 Modified to get Octave filename from form textbox
//  04/27/16 Modified per 'Octave 4.0.0 compatibility' post on CodeProject at
//          http://www.codeproject.com/Articles/342007/OctaveSharp-Running-GNU-Octave-with-Csharp

namespace MyWPFMagViewer2
{

    public class Octave
    {
        Process OctaveProcess { get; set; }
        private string OctaveEchoString { get; set; }
        public Octave(string PathToOctaveBinaries)
        {
            StartOctave(PathToOctaveBinaries, false);
        }

        public Octave(string PathToOctaveBinaries, bool CreateWindow)
        {
            StartOctave(PathToOctaveBinaries, CreateWindow);
        }

        string ptob;
        bool cw;
        private void StartOctave(string PathToOctaveBinaries, bool CreateWindow)
        {
            ptob = PathToOctaveBinaries;
            cw = CreateWindow;
            this.OctaveEchoString = Guid.NewGuid().ToString();
            OctaveProcess = new Process();
            ProcessStartInfo pi = new ProcessStartInfo();
            //if (PathToOctaveBinaries[PathToOctaveBinaries.Length - 1] != '\\')
            //    PathToOctaveBinaries = PathToOctaveBinaries + "\\";
            //pi.FileName = PathToOctaveBinaries + "octave.exe";
            pi.FileName = PathToOctaveBinaries;//now parameter contains full pathname
            pi.RedirectStandardInput = true;
            pi.RedirectStandardOutput = true;
            pi.RedirectStandardError = true;
            pi.UseShellExecute = false;
            pi.CreateNoWindow = !CreateWindow;
            pi.Verb = "open";
            //
            pi.WorkingDirectory = ".";
            OctaveProcess.StartInfo = pi;
            OctaveProcess.Start();
            OctaveProcess.OutputDataReceived += new DataReceivedEventHandler(OctaveProcess_OutputDataReceived);
            OctaveProcess.BeginOutputReadLine();
            OctaveEntryText = ExecuteCommand(null);
        }

        public string GetString(string retstr)
        {
            string rasp = ExecuteCommand(retstr);
            return rasp;
        }

        public double GetScalar(string scalar)
        {
            string rasp = ExecuteCommand(scalar, 30000);
            string val = rasp.Substring(rasp.LastIndexOf("\\") + 1).Trim();
            //return double.Parse(val);
            return double.Parse(val, CultureInfo.InvariantCulture);
        }

        public double[] GetVector(string vector)
        {
            string rasp = ExecuteCommand(vector, 30000);
            string[] lines = rasp.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int i = 0;
            //catam urmatorul entry
            List<double> data = new List<double>();
            while (i != lines.Length)
            {
                string line = lines[i];
                //if (line.Contains("through") || line.Contains("and")) {
                //if (line.Contains("Column") || line.Contains("Columns"))
                //05/13/16 bugfix - want the lines that *don't* contain "Columns"
                if (!(line.Contains("Column") || line.Contains("Columns"))) 
                {
                    //i++; //05/13/16 bugfix - this causes 2nd value to be skipped
                    line = lines[i];
                    string[] dataS = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int k = 0; k < dataS.Length; k++)
                    {
                        //05/07/16 have to guard against non-numerics here
                        double myNum = 0;
                        if (Double.TryParse(dataS[k], out myNum))
                        {
                            //data.Add(double.Parse(dataS[k]));
                            data.Add(double.Parse(dataS[k], CultureInfo.InvariantCulture));
                        }
                    }
                }
                i++;
            }
            //caz special in care a pus toate rezultatele pe o singura linie
            //special case in which all results are on a single line
            if (data.Count == 0)
            {
                string[] dataS = lines[lines.Length - 1].Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (dataS.Length != 0)
                    for (int k = 0; k < dataS.Length; k++)
                    {
                        //05/07/16 have to guard against non-numerics here
                        double myNum = 0;
                        if (Double.TryParse(dataS[k], out myNum))
                        {
                            //data.Add(double.Parse(dataS[k]));
                            data.Add(double.Parse(dataS[k], CultureInfo.InvariantCulture));
                        }
                    }
            }
            return data.ToArray();
        }

        public double[][] GetMatrix(string matrix)
        {
            //string rasp = ExecuteCommand(matrix);
            //aflam numarul de randuri
            string rasp = ExecuteCommand(matrix + "(:,1)", 30000);
            string[] lines = rasp.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            double[][] mat = new double[lines.Length - 1][];
            for (int i = 0; i < mat.Length; i++)
            {
                mat[i] = GetVector(matrix + "(" + (i + 1) + ",:)");
            }
            return mat;
        }

        StringBuilder SharedBuilder = new StringBuilder();
        ManualResetEvent OctaveDoneEvent = new ManualResetEvent(false);
        public string OctaveEntryText { get; internal set; }

        public void WorkThread(object o)
        {
            string command = (string)o;
            //SharedBuilder.Clear();
            SharedBuilder.Length = 0; //Net 3.5 doesn't support 'Clear()'
            OctaveDoneEvent.Reset();
            if (command != null)
            {
                OctaveProcess.StandardInput.WriteLine(command);
            }
            //ca sa avem referinta pentru output
            OctaveProcess.StandardInput.WriteLine("\"" + OctaveEchoString + "\"");
            OctaveDoneEvent.WaitOne();
        }
        public string ExecuteCommand(string command, int timeout)
        {
            command = "pwd();" + command;//added 04/27/16 per CodeProject post
            if (OctaveProcess.HasExited)
            {
                StartOctave(ptob, cw);
                if (OctaveRestarted != null) OctaveRestarted(this, EventArgs.Empty);
            }
            exitError = false;

            Thread tmp = new Thread(new ParameterizedThreadStart(WorkThread));
            tmp.Start(command);

            if (!tmp.Join(timeout))
            {
                tmp.Abort();
                throw new Exception("Octave timeout");
            }
            if (exitError)
            {
                throw new Exception(errorMessage);
            }
            return SharedBuilder.ToString();
        }
        public string ExecuteCommand(string command)
        {
            command = "pwd();" + command; //added 04/27/16 per CodeProject post
            Thread tmp = new Thread(new ParameterizedThreadStart(WorkThread));
            tmp.Start(command);

            tmp.Join();

            return SharedBuilder.ToString();
        }
        bool exitError = false;
        string errorMessage = null;
        void OctaveProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                //SharedBuilder.Clear();
                SharedBuilder.Length = 0;
                errorMessage = OctaveProcess.StandardError.ReadToEnd();
                SharedBuilder.Append("Octave has exited with the following error message: \r\n" + errorMessage);
                exitError = true;
                OctaveDoneEvent.Set();
                return;
            }
            if (e.Data.Trim() == "ans = " + OctaveEchoString)
                OctaveDoneEvent.Set();
            else
                SharedBuilder.Append(e.Data + "\r\n");
        }
        public event OctaveRestartedEventHandler OctaveRestarted;
        public delegate void OctaveRestartedEventHandler(object sender, EventArgs e);
    }
}
