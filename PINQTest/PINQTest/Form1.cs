using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PINQ;
using System.Diagnostics;
using System.Threading;

namespace PINQTest
{
    public partial class Form1 : Form
    {
        private IQueryable<GetAllCircuitByTSResult> powerData;
        private IEnumerable<IEnumerable<GetAllCircuitByTSResult>> clusteredData;
        private bool dataLoaded;
        private bool analysisDone;
        private int threadCount;
        private int totalThreads;
        private List<BudgetTestResult> allResults;
        private IEnumerable<IEnumerable<CircuitData>> allResultsAverage;
        private List<List<CircuitData>> testRuns;
        private Object analysisLock = new Object();
        private Object budgetLock = new Object();

        public Form1()
        {
            InitializeComponent();
            allResults = new List<BudgetTestResult>();
            dataLoaded = false;
            threadCount = 0;
        }

        private void Form1_Load(object sender, EventArgs e)
        {



        }

        /*private void DoAnalysis1()
        {
            SmartStarNamekDataContext ssndc = new SmartStarNamekDataContext();
            var all_data = ssndc.Environmentals.Select(i => i.insideTemp).AsQueryable();
            var a = ssndc.GetAllInsideTemp().OrderBy(i => i.insideTemp).ToList().AsQueryable();
            var b = ssndc.GetAllInsideTemp().AsQueryable().Average(i => i.insideTemp);
            var epsilon = (double)numericUpDown1.Value;
            var agent = new PINQAgentBudget(9000.0);
            var data = new PINQueryable<GetAllInsideTempResult>(a, agent);

            /*if (dataLoaded != true)
            {
                for (int i = 0; i < a.Count(); i++)
                    listBox1.Items.Add(a.ElementAt(i).insideTemp);

                label2.Text = "Data (" + listBox1.Items.Count + " items): ";
                dataLoaded = true;
            }

            try
            {
                var PINQAvg = data.NoisyAverage(epsilon, i => (double)i.insideTemp);
                var PINQMedian = data.NoisyMedian(epsilon, i => (double)i.insideTemp);
                var PINQCount = data.NoisyCount(epsilon);
                label1.Text = "LINQ (ssdc[all] -> LINQ Average): \n" + b +
                              "\n\nPINQ (ssdc[all] -> PINQ Noisy Average {" + epsilon + "}): \n" + PINQAvg.ToString() +
                              "\n\nPINQ (ssdc[all] -> PINQ Noisy Median {" + epsilon + "}): \n" + PINQMedian.ToString() +
                              "\n\nPINQ (ssdc[all] -> PINQ Noisy Count {" + epsilon + "}): \n" + PINQCount.ToString();
            }
            catch (Exception ex)
            {
                label1.Text = ex.Message;
            }
        }
*/
        private List<CircuitData> DoPINQAnalysis(double b) //Returns list of CircuitData
        {
            var PINQClusteredData = GetPINQAvgs(clusteredData, 3600, b); //3600 = 1 hour in seconds

            List<CircuitData> cd = new List<CircuitData>();

            foreach (var c in PINQClusteredData)
            {
                cd.Add(new CircuitData()
                {
                    budget = b,
                    TimestampUTC = c.TimestampUTC.ToString(),
                    RealPowerWatts = c.Power > 0 ? (double)c.Power : (double)(c.Power * -1)
                });
            }
            return cd;

        }

        IEnumerable<IEnumerable<GetAllCircuitByTSResult>> GetLINQAvgClusters(IEnumerable<GetAllCircuitByTSResult> data, int delta)
        {
            var cluster = new List<GetAllCircuitByTSResult>();
            foreach (var item in data.OrderBy(x => x.TimestampUTC))
            {
                if (cluster.Count > 0 && Int32.Parse(item.TimestampUTC) > Int32.Parse(cluster[0].TimestampUTC) + delta)
                {
                    //Take average of all parts of cluster
                    List<GetAllCircuitByTSResult> newItemOut = new List<GetAllCircuitByTSResult>();
                    GetAllCircuitByTSResult newItem = new GetAllCircuitByTSResult();
                    newItem.TimestampUTC = item.TimestampUTC;
                    newItem.Power = cluster.Average(i => i.Power);
                    newItemOut.Add(newItem);
                    cluster = newItemOut;
                    yield return cluster;
                    cluster = new List<GetAllCircuitByTSResult>();
                
                }
                cluster.Add(item);
            }
            if (cluster.Count > 0)
                yield return cluster;
        }

        IEnumerable<IEnumerable<GetAllCircuitByTSResult>> GetPINQAvgClusters(IEnumerable<GetAllCircuitByTSResult> data, int delta, double budget)
        {
            var cluster = new List<GetAllCircuitByTSResult>();
            var agent = new PINQAgentBudget(budget);
            var maxTimestamp = Int32.Parse(data.Max(x => x.TimestampUTC));
            var minTimestamp = Int32.Parse(data.Min(x => x.TimestampUTC));
            double clusterSize = (double)(maxTimestamp - minTimestamp) / 3600; //THIS MAY NEED CHANGING TO data.Count()??
            double singleRecordEpsilon = budget / data.Count();
            //label3.Text = "Real Cluster Size: " + clusterSize;
            foreach (var item in data)
            {
                //MOVE CLUSTERING OUTSIDE OF THREADS - BIG TIME HOG POTENTIALLY ************************************************************
                if ((cluster.Count > 0 && Int32.Parse(item.TimestampUTC) > Int32.Parse(cluster[0].TimestampUTC) + delta))
                {
                    double testClusterSize = cluster.Count();
                    double clusterEpsilon = singleRecordEpsilon * testClusterSize;
                    double testOldBudget = budget / clusterSize;
                    //Take average of all parts of cluster
                    var clusterData = new PINQueryable<PINQTest.GetAllCircuitByTSResult>(cluster.AsQueryable(), agent);
                    List<GetAllCircuitByTSResult> newItemOut = new List<GetAllCircuitByTSResult>();
                    GetAllCircuitByTSResult newItem = new GetAllCircuitByTSResult();

                    newItem.TimestampUTC = cluster.First().TimestampUTC;//item.TimestampUTC;
                    newItem.Power = (decimal)clusterData.NoisyAverage(clusterEpsilon, x => (double)x.Power); //budget / clusterSize
                    newItemOut.Add(newItem);

                    cluster = newItemOut;
                    yield return cluster;
                    cluster = new List<GetAllCircuitByTSResult>();
                }
                cluster.Add(item);
                //threadCount++;
                //label5.BeginInvoke(((Action)(() => label5.Text = threadCount.ToString())));
            }
            //if (cluster.Count > 0)
            //    yield return cluster;
        }

        IEnumerable<GetAllCircuitByTSResult> GetPINQAvgs(IEnumerable<IEnumerable<GetAllCircuitByTSResult>> clusteredData, int delta, double budget)
        {
            var agent = new PINQAgentBudget(budget);
            double singleRecordEpsilon = budget / powerData.Count();
            List<GetAllCircuitByTSResult> PINQAvgs = new List<GetAllCircuitByTSResult>();
            //label3.Text = "Real Cluster Size: " + clusterSize;
            foreach (var cluster in clusteredData)
            {
                    double clusterSize = cluster.Count();
                    double clusterEpsilon = singleRecordEpsilon * clusterSize;
                    //Take average of all parts of cluster
                    var clusterData = new PINQueryable<PINQTest.GetAllCircuitByTSResult>(cluster.AsQueryable(), agent);
                    //List<GetAllCircuitByTSResult> clusterAvgOut = new List<GetAllCircuitByTSResult>();
                    GetAllCircuitByTSResult clusterAvg = new GetAllCircuitByTSResult();

                    clusterAvg.TimestampUTC = cluster.First().TimestampUTC;//item.TimestampUTC;
                    clusterAvg.Power = (decimal)clusterData.NoisyAverage(clusterEpsilon, x => (double)x.Power); //budget / clusterSize
                    //clusterAvgOut.Add(clusterAvg);
                    PINQAvgs.Add(clusterAvg);
                        //cluster = new List<GetAllCircuitByTSResult>();
                //threadCount++;
                //label5.BeginInvoke(((Action)(() => label5.Text = threadCount.ToString())));
            }

            return PINQAvgs;
        }

        IEnumerable<IEnumerable<GetAllCircuitByTSResult>> GetTimestampClusters(IEnumerable<GetAllCircuitByTSResult> data, int timestampDelta)
        {
            var cluster = new List<GetAllCircuitByTSResult>();

            foreach (var item in data)
            {
                if ((cluster.Count > 0 && Int32.Parse(item.TimestampUTC) > Int32.Parse(cluster[0].TimestampUTC) + timestampDelta))
                {
                    yield return cluster;
                    cluster = new List<GetAllCircuitByTSResult>();
                }
                cluster.Add(item);
            }
        }
        /*delegate void AnalysisUIDelegate();
        delegate void BudgetUIDelegate();

        private void UpdateThreadsDone(int test)
        {

        }*/

        private void AnalysisThread(object sender, DoWorkEventArgs e)
        {
            totalThreads++;
            if (label3.InvokeRequired)
                label3.Invoke((Action)(() => label3.Text = totalThreads.ToString() + " threads started"));
            else
                label3.Text = totalThreads.ToString() + " threads started";

            if (progressBar1.InvokeRequired)
                progressBar1.Invoke((Action)(() => progressBar1.Value = (totalThreads + threadCount - 20) / 2));
            else
                progressBar1.Value = (totalThreads + threadCount - 20) / 2;

            object[] args = (object[])e.Argument;
            double budget = (double)args[0];
            BudgetTestResult btr = (BudgetTestResult)args[1];
            List<CircuitData> data = DoPINQAnalysis(budget);
            e.Result = new object[] { btr, data };
        }

        private void AnalysisThreadDone(object sender, RunWorkerCompletedEventArgs e)
        {
            lock (analysisLock)
            {
                threadCount++;
                if (label4.InvokeRequired)
                    label4.Invoke((Action)(() => label4.Text = threadCount.ToString() + " threads done"));
                else
                    label4.Text = threadCount.ToString() + " threads done";

                if (progressBar1.InvokeRequired)
                    progressBar1.Invoke((Action)(() => progressBar1.Value = (totalThreads + threadCount - 20) / 2));
                else
                    progressBar1.Value = (totalThreads + threadCount - 20) / 2;

                //label4.Text = threadCount.ToString() + " threads done";
                object[] args = (object[])e.Result;
                BudgetTestResult btr = (BudgetTestResult)args[0];
                List<CircuitData> data = (List<CircuitData>)args[1];

                btr.result.Add(data);
                //Thread.Sleep(500);
                if (threadCount == totalThreads)
                {
                    //END OF ANALYSIS
                    //Invoke averager
                    analysisDone = true;
                    if (label4.InvokeRequired)
                        label4.Invoke((Action)(() => label4.Text = "All threads done"));
                    else
                        label4.Text = threadCount.ToString() + "All threads done";

                    if (progressBar1.InvokeRequired)
                        progressBar1.Invoke((Action)(() => progressBar1.Value = 100));
                    else
                        progressBar1.Value = 100;
                    /*if (label4.InvokeRequired)
                        label4.Invoke((Action)(() => label4.Text = "Analysis done"));
                    else
                        label5.Text = "Analysis Done!";
                    */
                    #region chartcrap
                    /*chart1.Series.Clear();
                    var count = 0;
                    Random randomGen = new Random();
                    KnownColor[] names = (KnownColor[])Enum.GetValues(typeof(KnownColor));
                    foreach (var x in testRuns)
                    {
                        KnownColor randomColorName = names[randomGen.Next(names.Length)];
                        Color randomColor = Color.FromKnownColor(randomColorName);

                        var series = new System.Windows.Forms.DataVisualization.Charting.Series
                        {
                            Name = "series" + count,
                            Color = randomColor,
                            IsVisibleInLegend = false,
                            IsXValueIndexed = true,
                            ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Column,
                            ChartArea = "ChartArea1",
                            Enabled = false
                        };

                        foreach (var y in x)
                        {
                            series.Points.AddXY(y.TimestampUTC.ToString(), y.RealPowerWatts);
                        }
                        chart1.Series.Add(series);
                        count++;
                    }
                    chart1.DataBind();
                    chart1.Update();     */
                    #endregion
                }
            }
        }

        private void outputResults()
        {
            foreach (var item in allResultsAverage)
            {
                var budget = item.ElementAt(0).budget;
                TreeNode newNode = new TreeNode() { Text = budget.ToString()};
                foreach (var record in item)
                {
                    newNode.Nodes.Add(new TreeNode() { Text = "T: " + record.TimestampUTC + "  AP: " + record.RealPowerWatts});
                }
                treeView1.Nodes.Add(newNode);
            }

            foreach (var budget in allResults)
            {
                //var budget = item.ElementAt(0).budget;
                TreeNode newNodeBudget = new TreeNode() { Text = budget.result.ElementAt(0).ElementAt(0).budget.ToString() + " (Original)" };
                foreach (var item in budget.result)
                {
                    TreeNode newNode = new TreeNode() { Text = budget.result.IndexOf(item).ToString() };
                    foreach (var record in item)
                    {
                        newNode.Nodes.Add(new TreeNode() { Text = "T: " + record.TimestampUTC + "  AP: " + record.RealPowerWatts });
                    }
                    newNodeBudget.Nodes.Add(newNode);
                }
                treeView1.Nodes.Add(newNodeBudget);
            }
        }

        private void BudgetThread(object sender, DoWorkEventArgs e)
        {
            lock (budgetLock)
            {
                totalThreads++;
                if (label3.InvokeRequired)
                    label3.Invoke((Action)(() => label3.Text = totalThreads.ToString() + " threads started"));
                else
                    label3.Text = totalThreads.ToString() + " threads started";
                //object[] parameters = e.Argument as object[];

                double budget = (double)e.Argument;
                //int r = Int32.Parse((budget * 1000).ToString());
                //List<List<CircuitData>> list = (List<List<CircuitData>>)parameters.ElementAt(1);
                //Thread.Sleep(r);
                BudgetTestResult btr = new BudgetTestResult();
                btr.budget = budget;
                for (int i = 0; i < 10; i++)
                {
                    var bw = new BackgroundWorker();
                    object[] args = { budget, btr };

                    // define the event handlers
                    bw.DoWork += new DoWorkEventHandler(AnalysisThread);
                    bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(AnalysisThreadDone);
                    bw.RunWorkerAsync(args); // starts the background worker

                    //btr.result.Add(DoPINQAnalysis(budget));
                    //testRuns.Add(DoPINQAnalysis(budget));
                }
                //allResults.Add(btr);
                e.Result = btr;
            }
            //return DoPINQAnalysis(budget);
        }

        private void BudgetThreadDone(object sender, RunWorkerCompletedEventArgs e)
        {
            BudgetTestResult btr = (BudgetTestResult)e.Result;
            allResults.Add(btr);
            threadCount++;
            if (label4.InvokeRequired)
                label4.Invoke((Action)(() => label4.Text = threadCount.ToString() + " threads done"));
            else
                label4.Text = threadCount.ToString() + " threads done";

        }

        private IEnumerable<IEnumerable<CircuitData>> GetResultAverages()
        {
            List<List<CircuitData>> allAverages = new List<List<CircuitData>>();

            int dataSize = 0;
            int budgetCount = allResults.Count;
            int iterationsCount = allResults.ElementAt(0).result.Count;

            for (int i = 0; i < budgetCount; i++)
            {
                List<CircuitData> newList = new List<CircuitData>();
                for (int j = 0; j < iterationsCount; j++)
                {
                    dataSize = allResults.ElementAt(i).result.ElementAt(j).Count;

                    for (int k = 0; k < dataSize; k++)
                    {
                        //iterating through each data point in one iteration
                        //check for newList item at index k
                        var itemBudget = allResults.ElementAt(i).result.ElementAt(j).ElementAt(k).budget;
                        var itemTimestamp = allResults.ElementAt(i).result.ElementAt(j).ElementAt(k).TimestampUTC;
                        var itemWatts = allResults.ElementAt(i).result.ElementAt(j).ElementAt(k).RealPowerWatts;

                        if (newList.Count > k)
                        {
                            //found = add watts to newlist.watts
                            newList[k].RealPowerWatts += itemWatts;
                        }
                        else
                        {
                            //not found = create new list item at index k
                            CircuitData newCD = new CircuitData() { budget = itemBudget, 
                                                                    TimestampUTC = itemTimestamp,
                                                                    RealPowerWatts = itemWatts };
                            newList.Add(newCD);
                        }
                    }
                }
                allAverages.Add(newList);
                newList = new List<CircuitData>();
            }

            foreach (var budget in allAverages)
                foreach (var item in budget)
                    item.RealPowerWatts = item.RealPowerWatts / iterationsCount;

            return allAverages;

            /*foreach (var budgetResult in allResults)
            {
                var resultBudget = budgetResult.budget;
                var resultCount = budgetResult.result.ElementAt(0).Count;
                

                foreach (var item in budgetResult.result)
                {
                    double averagePower = 0.0;
                    for (int i = 0; i < budgetResult.result.Count; i++)
                    {
                        averagePower += item.ElementAt(i).RealPowerWatts;
                    }
                    averagePower = averagePower / budgetResult.result.Count;
                    var timestampUTC = item.ElementAt(0).TimestampUTC;
                    CircuitData averagePowerData = new CircuitData() { TimestampUTC = timestampUTC, 
                                                                       RealPowerWatts = averagePower, 
                                                                       budget = resultBudget };
                    newList.Add(averagePowerData);
                }
            }
            return allAverages;*/
        }
        
        private void button1_Click(object sender, EventArgs e)
        {
            SmartStarNamekDataContext ssndc = new SmartStarNamekDataContext();
            Stopwatch s = new Stopwatch();
            s.Start();

            this.Text = "Data Loading...";
            if (!dataLoaded)
            {
                powerData = ssndc.GetAllCircuitByTS().OrderBy(x => x.TimestampUTC).ToList().AsQueryable();
                clusteredData = GetTimestampClusters(powerData, 3600);
                dataLoaded = true;
            }

            testRuns = new List<List<CircuitData>>();
            double[] budgetList = new double[] { 0.001, 0.01, 0.1, 0.2, 0.5, 1, 1.5, 2, 5, 10 };

            analysisDone = false;
            foreach (var b in budgetList)
            {
                var bw = new BackgroundWorker();

                // define the event handlers
                bw.DoWork += new DoWorkEventHandler(BudgetThread);
                bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(BudgetThreadDone);
                bw.RunWorkerAsync(b); // starts the background worker
            }

            this.Text = "Threads started. Monitoring...";
        }

        private void button2_Click(object sender, EventArgs e)
        {
            allResultsAverage = GetResultAverages();
            outputResults();
        }

    }
}
