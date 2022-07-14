using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ValidationCADRes
{
    class CompareClass
    {
        int Width = 512;
        int Height = 512;
        int ImgSliceNum;

        int FN;
        int TP;
        int FP;
        Int32 LIDClesionNum;

        //ctor
        public CompareClass(string LIDC, string CAD)
        {
            Int16[] Ldata = LoadData(LIDC);
            Int16[] Cdata = LoadData(CAD);
            Int32 maxLesionID = getMaxLesionID(Ldata);
            this.LIDClesionNum = getNumofLesion(Ldata, maxLesionID);
            this.FN = getFN(Ldata, Cdata, maxLesionID, this.LIDClesionNum);

            getTPandFP(Ldata, Cdata);
        }
        //dtor
        ~CompareClass()
        {
        }

        //getter
        public Int32 fn
        {
            get { return this.FN; }
        }
        public Int32 tp
        {
            get { return this.TP; }
        }
        public Int32 fp
        {
            get { return this.FP; }
        }
        public Int32 lesionNum
        {
            get { return this.LIDClesionNum; }
        }


        private Int16[] LoadData(string path)
        {
            
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            ImgSliceNum = (Int32)(fs.Length / (Width*Height) / sizeof(Int16));
            var data = new Int16[Width * Height * ImgSliceNum];

            BinaryReader br = new BinaryReader(fs);
            for (int i = 0; i < Width*Height * ImgSliceNum; i++)
            {
                data[i] = br.ReadInt16();
            }
            br.Close();
            fs.Close();
            return data;
        }

        private Int32 getMaxLesionID(Int16[] data)
        {
            Int32 maxLesionID = 0;
            for (int i = 0; i < Width * Height * ImgSliceNum; i++)
            {
                if (data[i] > maxLesionID)
                {
                    maxLesionID = data[i];
                }
            }
            return maxLesionID;
        }


        private Int32 getNumofLesion(Int16[] Data, Int32 maxLesionID)
        {
            Int32[] LesionID = new Int32[maxLesionID + 1];
            Int32 NumofLesion = 0;
            for (int i = 0; i < Width * Height * ImgSliceNum; i++)
            {
                if (Data[i] > 0)
                {
                    if (LesionID[Data[i]] != 1)
                    {
                        LesionID[Data[i]] = 1;
                        NumofLesion++;
                    }
                }
            }

            return NumofLesion;
        }


        private Int32 getFN(Int16[] refData, Int16[] tgData, Int32 maxLesionID, Int32 numOfLesion)
        {
            //FNがないかどうかをチェックする
            Int32[] LesionID = new Int32[maxLesionID + 1];
            for (int i = 0; i < Width * Height * ImgSliceNum; i++)
            {
                if (refData[i] > 0)
                {
                    if(tgData[i] > 0)
                        LesionID[refData[i]] = 1;     //1 = とれている
                }
            }
            Int32 noFN = 0;
            for (int i = 1; i < maxLesionID + 1; i++)
            {
                if (LesionID[i] == 1)
                {
                    noFN++;
                }
            }

            return numOfLesion-noFN;
        }

        private void getTPandFP(Int16[] Ldata, Int16[] Cdata)
        {
            //領域数のカウント
            Int32[] CLesion = new Int32[5000];  //発見した領域ごとに、その領域のIDが入る
            Int32 CLesionCount = 0;
            for (int i = 0; i < Width * Height * ImgSliceNum; i++)
            {
                if (Cdata[i] > 0)
                {
                    //領域更新のチェック
                    Boolean flag = true;
                    for (int j = CLesionCount - 1; j >= 0; j--)
                    {
                        if (Cdata[i] == CLesion[j])
                        {
                            flag = false;
                            break;
                        }
                    }
                    if (flag)
                    {
                        CLesion[CLesionCount] = Cdata[i];
                        CLesionCount++;
                    }
                }
            }

            //TPかFPか
            Int32 TPnum = 0;
            for (int i = 0; i < Width * Height * ImgSliceNum; i++)
            {
                if(Ldata[i] > 0)
                {
                    if(Cdata[i] > 0)
                    {
                        //L,C両方が0以上だったら、CdataのIDをチェックし、TP数をインクリメントする
                        for (int n = 0; n < CLesionCount; n++)
                        {
                            if (Cdata[i] == CLesion[n])
                            {
                                CLesion[n] = -1;
                                TPnum++;
                            }
                        }
                    }
                }
            }
            this.TP = TPnum;
            this.FP = CLesionCount - TPnum;
            return;
        }

    }
}
