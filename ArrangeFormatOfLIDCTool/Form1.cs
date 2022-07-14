using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.IO;

using System.Runtime.InteropServices;




namespace ArrangeFormatOfLIDCTool
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        //Browseボタン
        private void button1_Click(object sender, EventArgs e)
        {
            //textboxに文字が入っていたら、初期パスをそれにする
            if (textBox1.Text != null)
                folderBrowserDialog1.SelectedPath = textBox1.Text;

            DialogResult dr = folderBrowserDialog1.ShowDialog();

            if (dr == System.Windows.Forms.DialogResult.OK)
            {
                textBox1.Text = folderBrowserDialog1.SelectedPath;
            }
            else
            {
                return;
            }
        }

        //Get XMLボタン
        private void button2_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
            //textBox Path以下のxmlファイルをすべて取得する
            IEnumerable<string> files = null;
            try
            {
                files = System.IO.Directory.EnumerateFiles(
                    textBox1.Text, "*.xml", System.IO.SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return;
            }

            //CT関連のxmlだけ抽出する
            #region
            foreach (string f in files)
            {
                XmlTextReader reader = null;
                try
                {
                    reader = new XmlTextReader(f);

                    //ストリームからノードを読み取る
                    while (reader.Read())
                    {
                        Boolean loopEndFlag = false;
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            switch (reader.LocalName)
                            {
                                case "TaskDescription":
                                    if (reader.ReadString() == "Second unblinded read")     //CTだけ抽出する
                                    {
                                        loopEndFlag = true;
                                        listBox1.Items.Add(f);
                                    }
                                    else
                                    {
                                        loopEndFlag = true;
                                    }
                                    break;
                                default:
                                    //none
                                    break;
                            }
                        }
                        //numfileがインクリメントされていたら、Whileをブレイク
                        if (loopEndFlag)
                            break;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                finally
                {
                    reader.Close();
                }
                
            }   //foreach
            #endregion

            label1.Text = (listBox1.Items.Count).ToString();

        }
       
        
        private void button3_Click(object sender, EventArgs e)
        {
            if (listBox1.Items.Count == 0)
                return;

            int counter = 0;
            foreach (string dcmdata in listBox1.Items)
            {
                //進捗
                label1.Text = (counter+1).ToString()+"/"+listBox1.Items.Count.ToString();
                Refresh();

                //===dcmの読み込み処理===
                //xmlファイルがあるフォルダ内のdcmファイルをすべて取得する
                #region
                string fPath = System.IO.Path.GetDirectoryName((listBox1.Items[counter]).ToString());
                IEnumerable<string> files = System.IO.Directory.EnumerateFiles(fPath, "*.dcm", System.IO.SearchOption.AllDirectories);
                #endregion

                //DICOM Data Setting Classのインスタンス作成
                #region
                var DDS = new DICOMDataSetting(files);
                #endregion

                //フラグセット
                #region
                //読み込むxmlファイル => listBox1.Items[]
                var RXML = new ReadingXML(DDS, listBox1.Items[counter].ToString());
                //書き込むrawファイル名 => 読み込んだフォルダの名前.raw
                string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(fPath)));
                FileStream wCti = new FileStream(folder + ".raw", FileMode.Create, FileAccess.Write);
                BinaryWriter bw = new BinaryWriter(wCti);
                for (int i = 0; i < DDS.numofslice; i++)
                {
                    for (int j = 0; j < DDS.height; j++)
                    {
                        for (int k = 0; k < DDS.width; k++)
                        {
                            bw.Write(RXML.data[k + j * DDS.width + i * DDS.width * DDS.height]);
                        }
                    }
                }
                bw.Close();
                wCti.Close();
                #endregion

                counter++;
            }
        }


        private void button4_Click(object sender, EventArgs e)
        {
            if (listBox1.Items.Count == 0)
                return;

            int counter = 0;
            foreach (string dcmdata in listBox1.Items)
            {
                label1.Text = (counter+1).ToString()+"/"+listBox1.Items.Count.ToString();
                Refresh();

                //===dcmの読み込み処理===
                //xmlファイルがあるフォルダ内のdcmファイルをすべて取得する
                #region
                string fPath = System.IO.Path.GetDirectoryName((listBox1.Items[counter]).ToString());
                IEnumerable<string> files = System.IO.Directory.EnumerateFiles(fPath, "*.dcm", System.IO.SearchOption.AllDirectories);
                #endregion

                //DICOM Data Setting Classのインスタンス作成
                #region
                var DDS = new DICOMDataSetting(files);
                #endregion

                //Pixel Dataの抽出・作成
                #region
                string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(fPath)));
                FileStream wCti = new FileStream(folder + ".raw", FileMode.Create, FileAccess.Write);
                BinaryWriter bw = new BinaryWriter(wCti);
                for (int i = 0; i < DDS.numofslice; i++)
                {
                    for (int j = 0; j < DDS.height; j++)
                    {
                        for (int k = 0; k < DDS.width; k++)
                        {
                            short value = (Int16)(DDS.pixelData[k + j * DDS.width + i * DDS.width * DDS.height] * DDS.rescaleSlope
                                + DDS.rescaleIntercept);
                            bw.Write(value);
                        }
                    }
                }
                bw.Close();
                wCti.Close();
                #endregion

                counter++;
            }

        }


        private void button5_Click(object sender, EventArgs e)
        {
            if (listBox1.Items.Count == 0)
                return;

            int counter = 0;
            //CSVファイルに書き込むときに使うEncoding
            System.Text.Encoding enc =
                System.Text.Encoding.GetEncoding("Shift_JIS");

            //書き込むファイルを開く
            System.IO.StreamWriter sr =
                new System.IO.StreamWriter("LIDCFOV.txt", false, enc);
            sr.Close();
            foreach (string dcmdata in listBox1.Items)
            {
                label1.Text = (counter + 1).ToString() + "/" + listBox1.Items.Count.ToString();
                Refresh();

                //===dcmの読み込み処理===
                //xmlファイルがあるフォルダ内のdcmファイルをすべて取得する
                #region
                string fPath = System.IO.Path.GetDirectoryName((listBox1.Items[counter]).ToString());
                IEnumerable<string> files = System.IO.Directory.EnumerateFiles(fPath, "*.dcm", System.IO.SearchOption.AllDirectories);
                #endregion

                //DICOM Data Setting Classのインスタンス作成
                #region
                var DDS = new DICOMDataSetting(files);
                #endregion

                //FOVの出力
                #region
                sr = new System.IO.StreamWriter("LIDCFOV.txt", true, enc);
                
                sr.WriteLine("{0}", DDS.fov);

                sr.Close();
                #endregion

                counter++;
            }
        }


    }
}
