using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dicom;
using Dicom.Imaging;
using Dicom.IO.Buffer;
using Dicom.Network;

using System.Xml;
using System.IO;
using Dicom.Imaging.Render;

namespace ArrangeFormatOfLIDCTool
{
    enum DeleteLabel { Candidate=1, Mask, Delete };


    class DICOMDataSetting
    {
        //Property
        Double[] SliceLoc;
        Int32 Width;
        Int32 Height;
        Double RescaleSlope;
        Double RescaleIntercept;
        Double PixelSpacing;
        Int16[] PixelData;
        Double FOV;

        //ctor
        public DICOMDataSetting(IEnumerable<string> files)
        {
            this.Width = 0;
            this.Height = 0;
            this.SliceLoc = getSliceLocation(files);
            getDICOMProperty(files);
            this.PixelData = getPixelData(files);
        }
        //dtor
        ~DICOMDataSetting()
        {
            
        }
        
        //Getter
        public int width
        {
            get { return this.Width; }
        }
        public int height
        {
            get { return this.Height; }
        }
        public int numofslice
        {
            get { return this.SliceLoc.Count(); }
        }
        public double rescaleSlope
        {
            get { return this.RescaleSlope; }
        }
        public double rescaleIntercept
        {
            get { return this.RescaleIntercept; }
        }
        public Int16[] pixelData
        {
            get { return this.PixelData; }
        }
        public double pixelSpacing
        {
            get { return this.PixelSpacing; }
        }
        public double fov
        {
            get { return this.FOV; }
        }
        //XMLのため
        public Double sliceLoc(int n)
        {
            return SliceLoc[n];
        }
        
            


        //Method
        private Double[] getSliceLocation(IEnumerable<string> files)
        {

            Double[] slicePos = new Double[files.Count()];
            int temp = 0;
            try
            {
                foreach (string fn in files)
                {
                    var dcmfile = DicomFile.Open(fn);
                    var sliceLocation = dcmfile.Dataset.Get<String>(DicomTag.SliceLocation);
                    slicePos[temp] = Double.Parse(sliceLocation);
                    temp++;
                }
                Array.Sort(slicePos);
                Array.Reverse(slicePos);
            }
            catch (Exception e)
            {
                throw e;
            }
            return slicePos;
        }
        private Int16[] getPixelData(IEnumerable<string> files)
        {
            //buffer
            var buff = new Int16[Width * Height * files.Count()];

            foreach (string fn in files)
            {
                var dcmfile = DicomFile.Open(fn);

                //スライス番号(instance numberのようなもの)の検索
                var sliceLoc = dcmfile.Dataset.Get<String>(DicomTag.SliceLocation);
                int slicePos = 0;
                for (int i = 0; i < files.Count(); i++)
                {
                    if (Double.Parse(sliceLoc) == this.SliceLoc[i])
                    {
                        slicePos = i;
                        break;
                    }
                }
                //ピクセルデータの取得
                try
                {
                    var header = DicomPixelData.Create(dcmfile.Dataset);
                    var pixelData = PixelDataFactory.Create(header, 0);
                    //UShort
                    if (pixelData is GrayscalePixelDataU16)
                    {
                        ushort[] tempPix = ((GrayscalePixelDataU16)pixelData).Data;
                        for (int i = 0; i < Width * Height; i++)
                            buff[i + slicePos * Width * Height] = (Int16)tempPix[i];
                    }
                    //Short
                    else
                    {
                        short[] tempPix = ((GrayscalePixelDataS16)pixelData).Data;
                        for (int i = 0; i < Width * Height; i++)
                            buff[i + slicePos * Width * Height] = tempPix[i];
                    }
                }
                catch (Exception e)
                {
                    throw e;
                }
            }
            return buff;
        }
        private void getDICOMProperty(IEnumerable<string> files)
        {
            try{
                var dcmfile = DicomFile.Open(files.First());
                //width
                this.Width = int.Parse(dcmfile.Dataset.Get<String>(DicomTag.Rows));
                //height
                this.Height = int.Parse(dcmfile.Dataset.Get<String>(DicomTag.Columns));
                //rescale slope
                this.RescaleSlope = double.Parse(dcmfile.Dataset.Get<String>(DicomTag.RescaleSlope));
                //rescale intercept
                this.RescaleIntercept = double.Parse(dcmfile.Dataset.Get<String>(DicomTag.RescaleIntercept));
                //pixel spacing
                this.PixelSpacing = double.Parse(dcmfile.Dataset.Get<String>(DicomTag.PixelSpacing));
                //FOV
                this.FOV = double.Parse(dcmfile.Dataset.Get<String>(DicomTag.ReconstructionDiameter));
            }
            catch(Exception e)
            {
                throw e;
            }
            return;
        }

    }


    class ReadingXML
    {
        //private
        Int16[] VolData;
        
        //ctor
        public ReadingXML(DICOMDataSetting dcm, string file)
        {
            //Mask作成
            this.VolData = MakeMaskFromXML(file, dcm);
            //2D上で5mmのトリミング
            Int16[] volMask = new Int16[dcm.width * dcm.height * dcm.numofslice];
            for (int i = 0; i < dcm.numofslice; i++)
            {
                Int16[] imgData = Trim5mm(VolData, dcm.width, dcm.height, i, dcm.pixelSpacing);
                for (int j = 0; j < dcm.width * dcm.height; j++)
                {
                    this.VolData[j + i * dcm.width * dcm.height] = imgData[j];
                }
            }
            //ラベル付け
            this.VolData = ArrangeLesionID(this.VolData, dcm.width, dcm.height, dcm.numofslice);
        }

        //dtor
        ~ReadingXML()
        {
        }

        //getter
        public Int16[] data
        {
            get { return this.VolData; }
        }

        //Method
        private Int16[] MakeMaskFromXML(string f, DICOMDataSetting dcm)
        {
            var xmlResData = new Int16[dcm.width*dcm.height*dcm.numofslice];
            if (File.Exists(f))
            {
                XmlTextReader reader = null;
                try
                {
                    reader = new XmlTextReader(f);
                    Int32 radiologistID = 0;    //XMLに記載されている4人分のradiologistIDを改めて振りなおす
                    Int32 noduleID = 0;         //XMLに記載されているnoduleIDを改めて振りなおす
                    Boolean noduleFlag = false;     //XMLにはnon noduleの情報も書かれているので、noduleだけ抽出するためのフラグ
                    int slicePos = 0;
                    //
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            switch(reader.LocalName)
                            {
                                case  "servicingRadiologistID":
                                    if (radiologistID == 0)
                                        radiologistID++;
                                    else
                                        radiologistID *= 2;
                                    noduleID = 0;   //radiologistが更新されたら、noduleIDをリセット
                                    break;
                                case "noduleID":
                                    noduleFlag = true;
                                    noduleID++;
                                    break;
                                case "nonNoduleID":
                                    noduleFlag = false;
                                    break;
                                case "imageZposition":
                                    string imageZpos = reader.ReadString();
                                    for(int i = 0; i < dcm.numofslice; i++)
                                    {
                                        if (double.Parse(imageZpos) == dcm.sliceLoc(i))
                                        {
                                            slicePos = i;
                                            break;
                                        }
                                    }
                                    break;
                                case "edgeMap":
                                    if(!noduleFlag)
                                        break;
                                    Int32 x = 0;
                                    Int32 y = 0;
                                    while (reader.Read())
                                    {
                                        switch (reader.LocalName)
                                        {
                                            case "xCoord":
                                                x = Int32.Parse(reader.ReadString());
                                                break;
                                            case "yCoord":
                                                y = Int32.Parse(reader.ReadString());
                                                break;
                                            case "edgeMap":
                                                xmlResData[slicePos * dcm.width*dcm.height + y * dcm.width + x]
                                                    = (Int16)(radiologistID+noduleID*16);
                                                goto EDGEEND;
                                            default:
                                                break;
                                        }
                                    }
                                    EDGEEND:
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    reader.Close();
                }
            }
            return xmlResData;
        }


        //計測
        //Trim 5mm
        //Summery
        //  slicePosのスライス内で、長径5mm未満の結節を削除したマスクを出力する。
        private Int16[] Trim5mm(Int16[] volumedata, int width, int height, int slicePos, double pixelSpacing)
        {
            var imgData = new Int16[width * height];
            for (int i = 0; i < width * height; i++)
            {
                if (volumedata[i + slicePos * width * height] > 0)
                    imgData[i] = (Int16)DeleteLabel.Candidate;
            }

            Int32 region = 0;   //領域の数
            for (int xy = 0; xy <  width * height; xy++ )
            {
                if (imgData[xy] == (Int16)DeleteLabel.Candidate)
                {
                    region++;
                    RegionGrowing RG = new RegionGrowing();
                    int[] extPIX = RG.regionGrowing2D(imgData, width, height, xy%width, xy/width, 1, Int16.MaxValue);
                    int[] Xsave = new int[width * height];
                    int[] Ysave = new int[width * height];
                    int kmax = 0;
                    for (int j = 0; j < RG.extRegion; j++)
                    {
                        int x1 = extPIX[j]%width;
                        int z1 = extPIX[j]/width;
                        imgData[z1*width+x1] = (Int16)DeleteLabel.Mask;	//結節候補をMaskにする

                        //8方向のいずれかが0になったら
                        if (imgData[z1 * width + x1 + 1] == 0 || imgData[z1 * width + x1 - 1] == 0 ||
                            imgData[(z1 + 1) * width + x1] == 0 || imgData[(z1 - 1) * width + x1] == 0 ||
                            imgData[(z1 - 1) * width + x1 - 1] == 0 || imgData[(z1 - 1) * width + x1 + 1] == 0 ||
                            imgData[(z1 + 1) * width + x1 - 1] == 0 || imgData[(z1 + 1) * width + x1 + 1] == 0)
                        {
                            //外周の座標を保存する。
                            Xsave[kmax] = x1;
                            Ysave[kmax] = z1;
                            kmax += 1;
                        }
                    }
                    //長径の探索、5mm未満もしくは>20mmを削除
                    #region
                    double rMAX = 1.0;
                    for (int j = 0; j < kmax; j++)
                    {
                        for (int i = 0; i < kmax; i++)
                        {
                            if (i == j) continue;
                            //長径の探索
                            //pixelの端から端を距離とするため、1を足す。
                            double L = 1.0 + Math.Sqrt((double)((Xsave[i] - Xsave[j]) * (Xsave[i] - Xsave[j]) + (Ysave[i] - Ysave[j]) * (Ysave[i] - Ysave[j])));
                            if (rMAX < L)
                            {
                                rMAX = L;
                            }
                        }
                    }
                    if (rMAX * pixelSpacing < 5)
                    {
                        for (int j = 0; j < RG.extRegion; j++)
                        {
                            imgData[extPIX[j]] = (Int16)DeleteLabel.Delete;
                        }
                    }
                    else if (rMAX * pixelSpacing > 20)
                    {
                        for (int j = 0; j < RG.extRegion; j++)
                        {
                            imgData[extPIX[j]] = (Int16)DeleteLabel.Delete;
                        }
                    }
                    #endregion
                }   //if imagedata>0
            }   //forxy
            //for (int xy = 0; xy < width * height; xy++)
            //{
            //    imgData[xy] *= (Int16)(-1);
            //}

            return imgData;
        }

        //ラベル付け
        //Summery
        private Int16[] ArrangeLesionID(Int16[] volumeData, int width, int height, int slicenum)
        {
            Int32 Label = 0;
            //ラスター操作し、発見した画素に対して3DRegionGrowing
            //画素に含まれるnoduleIDをすべて抽出する
            for (int xyz = 0; xyz < width * height * slicenum; xyz++)
            {
                if (volumeData[xyz] <= 0)
                    continue;

                
                RegionGrowing RG = new RegionGrowing();
                Int32[] extPIX = RG.regionGrowing3D(volumeData, width, height, slicenum, xyz, 1, Int16.MaxValue);
                
                bool del_flag = false;
                for (int j = 0; j < RG.extRegion; j++)
                {
                    if (volumeData[extPIX[j]] == (Int16)DeleteLabel.Delete)
                        del_flag = true;
                }
                if (del_flag)
                {
                    for (int j = 0; j < RG.extRegion; j++)
                    {
                        volumeData[extPIX[j]] = 0;
                    }
                }
                else
                {
                    Label++;
                    for (int j = 0; j < RG.extRegion; j++)
                    {
                        volumeData[extPIX[j]] = (Int16)(-Label);	//結節候補を-labelにする
                    }
                }
            }
            for (int xyz = 0; xyz < width * height * slicenum; xyz++)
            {
                volumeData[xyz] *= (Int16)(-1);
            }
            return volumeData;
        }
    }


    public class RegionGrowing
    {
        Int32 ExtRegion;



        public Int32 extRegion
        {
            get { return ExtRegion; }
        }

        public int[] regionGrowing2D(Int16[] bitmask, int width, int height, int x0, int y0, int th1, int th2)
        {
            var maskData = new Int32[width * height];
            for (int i = 0; i < width * height; i++)
            {
                maskData[i] = (Int32)bitmask[i];
            }
            int[] extPIX = new int[width * height];
            //定義：8近傍
            Int32[] neigh = new Int32[] { 1, 1 + width, width, -1 + width, -1, -1 - width, -width, 1 - width };

            int seed = x0 + y0 * width;	//シード点

            //エラーチェック
            if (maskData[seed] == 0)
            {
                this.ExtRegion = 0;
                Exception ex = new Exception("シード点の画素値が0です。");
                throw ex;
            }
            if (th1 <= 0 || th2 <= 0)
            {
                this.ExtRegion = 0;
                Exception ex = new Exception("閾値が0以下です。");
                throw ex;
            }

            //初期化
            extPIX[0] = seed;
            maskData[seed] = 0;
            int tail = 1;
            //tailの成長速度をheadが超えるまで(tailが変化するので注意)
            for (int head = 0; head < tail; head++)
            {
                int tempx = extPIX[head] % width;
                int tempy = extPIX[head] / width;
                //画像の端なら
                if ((tempx >= width - 1) || (tempx <= 0) || (tempy >= height - 1) || (tempy <= 0))
                {
                    continue;
                }
                for (int i = 0; i < 8; i++)
                {
                    int judgePos = extPIX[head] + neigh[i];
                    //周辺画素で、DOフラグチェック
                    if (maskData[judgePos] >= th1 && maskData[judgePos] <= th2)
                    {
                        extPIX[tail] = judgePos;
                        maskData[judgePos] = 0;
                        tail++;
                    }
                }
            }
            this.ExtRegion = tail;
            return extPIX;
        }

        public int[] regionGrowing3D(Int16[] volData, int width, int height, int slicenum, int xyz0, int th1, int th2)
        {
            var maskData = new Int32[width * height * slicenum];
            for (int i = 0; i < width * height * slicenum; i++)
            {
               maskData[i] = (Int32)volData[i];
            }
            int[] extPIX = new int[width * height * slicenum];
            //定義：8+2近傍
            Int32[] neigh = new Int32[] { 1, 1 + width, width, -1 + width, -1, -1 - width, -width, 1 - width,
                -width * height, width * height };

            int seed = xyz0;	//シード点

            //エラーチェック
            if (maskData[seed] == 0)
            {
                this.ExtRegion = 0;
                Exception ex = new Exception("シード点の画素値が0です。");
                throw ex;
            }
            if (th1 <= 0 || th2 <= 0)
            {
                this.ExtRegion = 0;
                Exception ex = new Exception("閾値が0以下です。");
                throw ex;
            }

            //初期化
            extPIX[0] = seed;
            maskData[seed] = 0;
            int tail = 1;
            //tailの成長速度をheadが超えるまで(tailが変化するので注意)
            for (int head = 0; head < tail; head++)
            {
                int tempx = (extPIX[head] % (width * height)) % width;
                int tempy = (extPIX[head] % (width * height)) / width;
                int tempz = extPIX[head] / (width * height);
                //画像の端なら
                if ((tempx >= width - 1) || (tempx <= 0) || (tempy >= height - 1) || (tempy <= 0) || (tempz >= slicenum - 1) || (tempz <= 0))
                {
                    continue;
                }
                for (int i = 0; i < 10; i++)
                {
                    int judgePos = extPIX[head] + neigh[i];
                    //周辺画素で、DOフラグチェック
                    if (maskData[judgePos] >= th1 && maskData[judgePos] <= th2)
                    {
                        extPIX[tail] = judgePos;
                        maskData[judgePos] = 0;
                        tail++;
                    }
                }
            }
            this.ExtRegion = tail;
            return extPIX;
        }
    }
}
