using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuperBMDLib.Geometry;
using GameFormatReader.Common;
using Assimp;
using SuperBMDLib.Geometry.Enums;
using SuperBMDLib.Util;
using SuperBMDLib.Rigging;
using OpenTK;
using SuperBMD.source.Geometry.Enums;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Converters;

namespace SuperBMDLib.BMD
{
    public class SHP1
    {
        public List<Shape> Shapes { get; private set; }
        public List<int> RemapTable { get; private set; }

        private SHP1()
        {
            Shapes = new List<Shape>();
            RemapTable = new List<int>();
        }

        private SHP1(EndianBinaryReader reader, int offset, BMDInfo modelstats=null)
        {
            Shapes = new List<Shape>();
            RemapTable = new List<int>();

            reader.BaseStream.Seek(offset, System.IO.SeekOrigin.Begin);
            reader.SkipInt32();
            int shp1Size = reader.ReadInt32();
            int entryCount = reader.ReadInt16();
            reader.SkipInt16();

            if (modelstats != null) {
                modelstats.SHP1Size = shp1Size;
            }

            int shapeHeaderDataOffset = reader.ReadInt32();
            int shapeRemapTableOffset = reader.ReadInt32();
            int unusedOffset = reader.ReadInt32();
            int attributeDataOffset = reader.ReadInt32();
            int matrixIndexDataOffset = reader.ReadInt32();
            int primitiveDataOffset = reader.ReadInt32();
            int matrixDataOffset = reader.ReadInt32();
            int PacketInfoDataOffset = reader.ReadInt32();

            reader.BaseStream.Seek(offset + shapeRemapTableOffset, System.IO.SeekOrigin.Begin);

            // Remap table
            for (int i = 0; i < entryCount; i++)
                RemapTable.Add(reader.ReadInt16());

            int highestIndex = J3DUtility.GetHighestValue(RemapTable);

            // Packet data
            List<Tuple<int, int>> packetData = new List<Tuple<int, int>>(); // <packet size, packet offset>
            int packetDataCount = (shp1Size - PacketInfoDataOffset) / 8;
            reader.BaseStream.Seek(PacketInfoDataOffset + offset, System.IO.SeekOrigin.Begin);

            for (int i = 0; i < packetDataCount; i++)
            {
                packetData.Add(new Tuple<int, int>(reader.ReadInt32(), reader.ReadInt32()));
            }

            // Matrix data
            List<Tuple<int, int>> matrixData = new List<Tuple<int, int>>(); // <index count, start index>
            List<int[]> matrixIndices = new List<int[]>();

            int matrixDataCount = (PacketInfoDataOffset - matrixDataOffset) / 8;
            reader.BaseStream.Seek(matrixDataOffset + offset, System.IO.SeekOrigin.Begin);

            for (int i = 0; i < matrixDataCount; i++)
            {
                reader.SkipInt16();
                matrixData.Add(new Tuple<int, int>(reader.ReadInt16(), reader.ReadInt32()));
            }

            for (int i = 0; i < matrixDataCount; i++)
            {
                reader.BaseStream.Seek(offset + matrixIndexDataOffset + (matrixData[i].Item2 * 2), System.IO.SeekOrigin.Begin);
                int[] indices = new int[matrixData[i].Item1];

                for (int j = 0; j < matrixData[i].Item1; j++)
                    indices[j] = reader.ReadInt16();

                matrixIndices.Add(indices);
            }

            // Shape data
            List<Shape> tempShapeList = new List<Shape>();
            reader.BaseStream.Seek(offset + shapeHeaderDataOffset, System.IO.SeekOrigin.Begin);

            for (int i = 0; i < highestIndex + 1; i++)
            {
                MatrixType matrixType = (MatrixType)reader.ReadByte();
                reader.SkipByte();

                int packetCount = reader.ReadInt16();
                int shapeAttributeOffset = reader.ReadInt16();
                int shapeMatrixDataIndex = reader.ReadInt16();
                int firstPacketIndex = reader.ReadInt16();
                reader.SkipInt16();

                BoundingVolume shapeVol = new BoundingVolume(reader);

                long curOffset = reader.BaseStream.Position;
                int totalPacketSize = 0;
                ShapeVertexDescriptor descriptor = new ShapeVertexDescriptor(reader, offset + attributeDataOffset + shapeAttributeOffset);

                List<Packet> shapePackets = new List<Packet>();
                for (int j = 0; j < packetCount; j++)
                {
                    int packetSize = packetData[j + firstPacketIndex].Item1;
                    int packetOffset = packetData[j + firstPacketIndex].Item2;
                    totalPacketSize += packetSize;
                    Packet pack;
                    if (j + firstPacketIndex < matrixIndices.Count)
                    {
                        pack = new Packet(packetSize, packetOffset + primitiveDataOffset + offset, matrixIndices[j + firstPacketIndex]);
                    }
                    else
                    {
                        //Fixes the exporting of older models made with tools like obj2bdl
                        pack = new Packet(packetSize, packetOffset + primitiveDataOffset + offset, matrixIndices[0]);
                    }
                    pack.ReadPrimitives(reader, descriptor);

                    shapePackets.Add(pack);
                }

                tempShapeList.Add(new Shape(descriptor, shapeVol, shapePackets, matrixType));

                reader.BaseStream.Seek(curOffset, System.IO.SeekOrigin.Begin);
            }

            for (int i = 0; i < entryCount; i++)
                Shapes.Add(tempShapeList[RemapTable[i]]);

            reader.BaseStream.Seek(offset + shp1Size, System.IO.SeekOrigin.Begin);
        }

        private SHP1(Assimp.Scene scene, VertexData vertData, Dictionary<string, int> boneNames, EVP1 envelopes, DRW1 partialWeight,
            string tristripMode = "static", bool include_normals = false, bool degenerateTriangles = false, bool addEnvAttrib = false, 
            bool flip_faces = false, List<Materials.Material> mat_presets = null)
        {
            Shapes = new List<Shape>();
            RemapTable = new List<int>();

            if (flip_faces)
            {
                Console.WriteLine("Flip Faces enabled. Winding order of faces has been turned around (Affects culling but not vertex normals)");
            }

            foreach (Mesh mesh in scene.Meshes)
            {
                Console.Write(mesh.Name+": ");
                Shape meshShape;
                string matName = scene.Materials[mesh.MaterialIndex].Name;

                /*if (mesh.Name.Contains("Bill0")) {
                    meshShape = new Shape(0); // Matrix Type 0, unknown
                }*/
                if (mesh.Name.Contains("BillXY")) {
                    meshShape = new Shape(MatrixType.BillboardXY); // Matrix Type 1, XY Billboard
                    Console.WriteLine("Billboarding on the X & Y axis");
                }
                else if (mesh.Name.Contains("BillX")) {
                    meshShape = new Shape(MatrixType.BillboardX); // Matrix Type 2, X Billboard, i.e. the X axis is always turned towards camera
                    Console.WriteLine("Billboarding on the X axis");
                }
                else {
                    meshShape = new Shape(); // Matrix Type 3, normal
                    Console.WriteLine("Normal Mesh");
                }

                // Force a mesh in an otherwise rigged model to be "unrigged"
                bool forceUnweighted = mesh.Name.Contains("_NoWeights");

                if (forceUnweighted) { 
                    Console.WriteLine(String.Format("\nMesh {0} forced to be unweighted.", mesh.Name));
                    meshShape.SetDescriptorAttributes(mesh, 1, include_normals, addEnvAttrib, matName, mat_presets);
                }
                else
                {
                    meshShape.SetDescriptorAttributes(mesh, boneNames.Count, include_normals, addEnvAttrib, matName, mat_presets);
                }

                if (boneNames.Count > 1 && !forceUnweighted)
                    meshShape.ProcessVerticesWithWeights(mesh, vertData, boneNames, envelopes, partialWeight, tristripMode == "all", degenerateTriangles, flip_faces);
                else
                {
                    

                    var jointindex = 0;
                    var transformVerts = false;
                    if (mesh.Name.Contains("_NoWeights_"))
                    {
                        string[] result = mesh.Name.Split(new string[] {"_NoWeights_" }, StringSplitOptions.None);
                        if (result.Length == 1)
                        {
                            throw new Exception(String.Format("Missing Bone Name after _NoWeights_: {0}", mesh.Name));
                        }
                        else if (!boneNames.ContainsKey(result[1]))
                        {
                            throw new Exception(String.Format("No such bone name: {0}", result[1]));
                        }
                        jointindex = boneNames[result[1]];
                        transformVerts = true;
                    }

                    partialWeight.WeightTypeCheck.Add(false);
                    partialWeight.Indices.Add(jointindex);

                    meshShape.ProcessVerticesWithoutWeights(mesh, vertData, envelopes, degenerateTriangles, jointindex, partialWeight.Indices.Count-1, transformVerts, flip_faces);
                    
                    Console.WriteLine("Assigned joint index {0} to {1}", jointindex, mesh.Name);
                    
                }

                Shapes.Add(meshShape);
                Console.WriteLine();
            }
        }

        public static SHP1 Create(EndianBinaryReader reader, int offset, BMDInfo modelstats=null)
        {
            return new SHP1(reader, offset, modelstats);
        }

        public static SHP1 Create(Scene scene, Dictionary<string, int> boneNames, VertexData vertData, EVP1 evp1, DRW1 drw1,
            string tristrip_mode = "static", bool include_normals = false, bool degenerateTriangles = false, bool addEnvAttrib = false, 
            bool flip_faces = false, List<Materials.Material> mat_presets = null)
        {
            SHP1 shp1 = new SHP1(scene, vertData, boneNames, evp1, drw1, tristrip_mode, include_normals, degenerateTriangles, addEnvAttrib, flip_faces, mat_presets);

            return shp1;
        }

        public void SetVertexWeights(EVP1 envelopes, DRW1 drawList)
        {
            for (int i = 0; i < Shapes.Count; i++)
            {
                for (int j = 0; j < Shapes[i].Packets.Count; j++)
                {
                    foreach (Primitive prim in Shapes[i].Packets[j].Primitives)
                    {
                        foreach (Vertex vert in prim.Vertices)
                        {
                            if (Shapes[i].Descriptor.CheckAttribute(GXVertexAttribute.PositionMatrixIdx))
                            {
                                int drw1Index = Shapes[i].Packets[j].MatrixIndices[(int)vert.PositionMatrixIDxIndex];
                                int curPacketIndex = j;
                                while (drw1Index == -1)
                                {
                                    curPacketIndex--;
                                    drw1Index = Shapes[i].Packets[curPacketIndex].MatrixIndices[(int)vert.PositionMatrixIDxIndex];
                                }

                                if (drawList.WeightTypeCheck[(int)drw1Index])
                                {
                                    int evp1Index = drawList.Indices[(int)drw1Index];
                                    vert.SetWeight(envelopes.Weights[evp1Index]);
                                }
                                else
                                {
                                    Weight vertWeight = new Weight();
                                    vertWeight.AddWeight(1.0f, drawList.Indices[(int)drw1Index]);
                                    vert.SetWeight(vertWeight);
                                }
                            }
                            else
                            {
                                Weight vertWeight = new Weight();
                                vertWeight.AddWeight(1.0f, drawList.Indices[Shapes[i].Packets[j].MatrixIndices[0]]);
                                vert.SetWeight(vertWeight);
                            }
                        }
                    }
                }
            }
        }

        public void FillScene(Scene scene, VertexData vertData, List<Rigging.Bone> flatSkeleton, List<Matrix4> inverseBindMatrices)
        {
            for (int i = 0; i < Shapes.Count; i++)
            {
                

                int vertexID = 0;
                Shape curShape = Shapes[i];

                Console.Write("Mesh " + i + ": ");
                string meshname = $"mesh_{ i }";

                switch (curShape.MatrixType) {
                    case MatrixType.BillboardX:
                        meshname += "_BillX";
                        Console.Write("X Billboarding Detected! ");
                        break;
                    case MatrixType.BillboardXY:
                        meshname += "_BillXY";
                        Console.Write("XY Billboarding Detected! ");
                        break;
                    default:
                        break;
                }
                
                Mesh mesh = new Mesh(meshname, PrimitiveType.Triangle);
                mesh.MaterialIndex = i;
                bool[] outOfRangeCoords_detected = {false, false, false, false, false, false, false, false};
                List<String> usedbones = new List<String>();

                foreach (Packet pack in curShape.Packets)
                {
                    foreach (Primitive prim in pack.Primitives)
                    {
                        List<Vertex> triVertices = J3DUtility.PrimitiveToTriangles(prim);

                        for (int triIndex = 0; triIndex < triVertices.Count; triIndex += 3)
                        {
                            Face newFace = new Face(new int[] { vertexID + 2, vertexID + 1, vertexID });
                            mesh.Faces.Add(newFace);

                            for (int triVertIndex = 0; triVertIndex < 3; triVertIndex++)
                            {
                                Vertex vert = triVertices[triIndex + triVertIndex];

                                for (int j = 0; j < vert.VertexWeight.WeightCount; j++)
                                {
                                    Rigging.Bone curWeightBone = flatSkeleton[vert.VertexWeight.BoneIndices[j]];
                                    if (!usedbones.Contains(String.Format("{0}-{1}", curWeightBone.Name, vert.VertexWeight.BoneIndices[j])))
                                    {
                                        usedbones.Add(String.Format("{0}-{1}", curWeightBone.Name, vert.VertexWeight.BoneIndices[j]));
                                    }
                                    int assBoneIndex = mesh.Bones.FindIndex(x => x.Name == curWeightBone.Name);

                                    if (assBoneIndex == -1)
                                    {
                                        Assimp.Bone newBone = new Assimp.Bone();
                                        newBone.Name = curWeightBone.Name;
                                        newBone.OffsetMatrix = curWeightBone.InverseBindMatrix.ToMatrix4x4();
                                        mesh.Bones.Add(newBone);
                                        assBoneIndex = mesh.Bones.IndexOf(newBone);
                                    }

                                    mesh.Bones[assBoneIndex].VertexWeights.Add(new VertexWeight(vertexID, vert.VertexWeight.Weights[j]));
                                }

                                OpenTK.Vector3 posVec = vertData.Positions[(int)vert.GetAttributeIndex(GXVertexAttribute.Position)];
                                OpenTK.Vector4 openTKVec = new Vector4(posVec.X, posVec.Y, posVec.Z, 1);

                                Vector3D vertVec = new Vector3D(openTKVec.X, openTKVec.Y, openTKVec.Z);

                                if (vert.VertexWeight.WeightCount == 1)
                                {
                                    if (inverseBindMatrices.Count > vert.VertexWeight.BoneIndices[0])
                                    {
                                        Matrix4 test = inverseBindMatrices[vert.VertexWeight.BoneIndices[0]].Inverted();
                                        test.Transpose();
                                        Vector4 trans = OpenTK.Vector4.Transform(openTKVec, test);
                                        vertVec = new Vector3D(trans.X, trans.Y, trans.Z);
                                    }
                                    else
                                    {
                                        Vector4 trans = OpenTK.Vector4.Transform(openTKVec, flatSkeleton[vert.VertexWeight.BoneIndices[0]].TransformationMatrix);
                                        vertVec = new Vector3D(trans.X, trans.Y, trans.Z);
                                    }
                                }

                                mesh.Vertices.Add(vertVec);

                                if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.Normal))
                                {
                                    OpenTK.Vector3 nrmVec = vertData.Normals[(int)vert.NormalIndex];
                                    OpenTK.Vector4 openTKNrm = new Vector4(nrmVec.X, nrmVec.Y, nrmVec.Z, 1);
                                    Vector3D vertNrm = new Vector3D(nrmVec.X, nrmVec.Y, nrmVec.Z);

                                    if (vert.VertexWeight.WeightCount == 1)
                                    {
                                        if (inverseBindMatrices.Count > vert.VertexWeight.BoneIndices[0])
                                        {
                                            Matrix4 test = inverseBindMatrices[vert.VertexWeight.BoneIndices[0]].Inverted();
                                            vertNrm = Vector3.TransformNormalInverse(nrmVec, test).ToVector3D();
                                        }
                                        else
                                        {
                                            Vector4 trans = OpenTK.Vector4.Transform(openTKNrm, flatSkeleton[vert.VertexWeight.BoneIndices[0]].TransformationMatrix);
                                            vertNrm = new Vector3D(trans.X, trans.Y, trans.Z);
                                        }
                                    }

                                    mesh.Normals.Add(vertNrm);
                                }

                                if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.Color0))
                                    mesh.VertexColorChannels[0].Add(vertData.Color_0[(int)vert.Color0Index].ToColor4D());

                                if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.Color1))
                                    mesh.VertexColorChannels[1].Add(vertData.Color_1[(int)vert.Color1Index].ToColor4D());

                                for (int texCoordNum = 0; texCoordNum < 8; texCoordNum++)
                                {
                                    if (curShape.Descriptor.CheckAttribute(GXVertexAttribute.Tex0 + texCoordNum))
                                    {
                                        Vector3D texCoord = new Vector3D();
                                        switch (texCoordNum)
                                        {
                                            case 0:
                                                if (vertData.TexCoord_0.Count <= (int)vert.TexCoord0Index)
                                                {
                                                    outOfRangeCoords_detected[0] = true;
                                                }
                                                else
                                                {
                                                    texCoord = vertData.TexCoord_0[(int)vert.TexCoord0Index].ToVector2D();
                                                }
                                                break;
                                            case 1:
                                                if (vertData.TexCoord_1.Count <= (int)vert.TexCoord1Index)
                                                {
                                                    outOfRangeCoords_detected[1] = true;
                                                }
                                                else
                                                {
                                                    texCoord = vertData.TexCoord_1[(int)vert.TexCoord1Index].ToVector2D();
                                                }
                                                break;
                                            case 2:
                                                if (vertData.TexCoord_2.Count <= (int)vert.TexCoord2Index)
                                                {
                                                    outOfRangeCoords_detected[2] = true;
                                                }
                                                else
                                                {
                                                    texCoord = vertData.TexCoord_2[(int)vert.TexCoord2Index].ToVector2D();
                                                }
                                                break;
                                            case 3:
                                                if (vertData.TexCoord_3.Count <= (int)vert.TexCoord3Index)
                                                {
                                                    outOfRangeCoords_detected[3] = true;
                                                }
                                                else
                                                {
                                                    texCoord = vertData.TexCoord_3[(int)vert.TexCoord3Index].ToVector2D();
                                                }
                                                break;
                                            case 4:
                                                if (vertData.TexCoord_4.Count <= (int)vert.TexCoord4Index)
                                                {
                                                    outOfRangeCoords_detected[4] = true;
                                                }
                                                else
                                                {
                                                    texCoord = vertData.TexCoord_4[(int)vert.TexCoord4Index].ToVector2D();
                                                }
                                                break;
                                            case 5:
                                                if (vertData.TexCoord_5.Count <= (int)vert.TexCoord5Index)
                                                {
                                                    outOfRangeCoords_detected[5] = true;
                                                }
                                                else
                                                {
                                                    texCoord = vertData.TexCoord_5[(int)vert.TexCoord5Index].ToVector2D();
                                                }
                                                break;
                                            case 6:
                                                if (vertData.TexCoord_6.Count <= (int)vert.TexCoord6Index)
                                                {
                                                    outOfRangeCoords_detected[6] = true;
                                                }
                                                else
                                                {
                                                    texCoord = vertData.TexCoord_6[(int)vert.TexCoord6Index].ToVector2D();
                                                }
                                                break;
                                            case 7:
                                                if (vertData.TexCoord_7.Count <= (int)vert.TexCoord7Index)
                                                {
                                                    outOfRangeCoords_detected[7] = true;
                                                }
                                                else { 
                                                    texCoord = vertData.TexCoord_7[(int)vert.TexCoord7Index].ToVector2D();
                                                }
                                                break;
                                        }

                                        mesh.TextureCoordinateChannels[texCoordNum].Add(texCoord);
                                    }
                                }

                                vertexID++;
                            }
                        }
                    }
                    Console.Write("...");
                }
                for (int j = 0; j < 8; j++)
                {
                    if (outOfRangeCoords_detected[j] == true)
                    {
                        Console.WriteLine("Warning, out of range Tex Coords detected on channel {0}", j);
                    }
                }
                scene.Meshes.Add(mesh);
                Console.Write("✓");
                Console.WriteLine();
                Console.WriteLine("Used bones by {0}:", mesh.Name);
                foreach( string name in usedbones)
                {
                    Console.Write(name);
                    Console.Write(",");
                }
                Console.WriteLine();
            }
        }

        public void Write(EndianBinaryWriter writer)
        {
            List<Tuple<ShapeVertexDescriptor, int>> descriptorOffsets; // Contains the offsets for each unique vertex descriptor
            List<Tuple<Packet, int>> packetMatrixOffsets; // Contains the offsets for each packet's matrix indices
            List<Tuple<int, int>> packetPrimitiveOffsets; // Contains the offsets for each packet's first primitive

            long start = writer.BaseStream.Position;

            writer.Write("SHP1".ToCharArray());
            writer.Write(0); // Placeholder for section offset
            writer.Write((short)Shapes.Count);
            writer.Write((short)-1);

            writer.Write(44); // Offset to shape header data. Always 48

            for (int i = 0; i < 7; i++)
                writer.Write(0);

            foreach (Shape shp in Shapes)
            {
                shp.Write(writer);
            }

            // Remap table offset
            writer.Seek((int)(start + 16), System.IO.SeekOrigin.Begin);
            writer.Write((int)(writer.BaseStream.Length - start));
            writer.Seek((int)(writer.BaseStream.Length), System.IO.SeekOrigin.Begin);

            for (int i = 0; i < Shapes.Count; i++)
                writer.Write((short)i);

            StreamUtility.PadStreamWithString(writer, 32);

            // Attribute descriptor data offset
            writer.Seek((int)(start + 24), System.IO.SeekOrigin.Begin);
            writer.Write((int)(writer.BaseStream.Length - start));
            writer.Seek((int)(writer.BaseStream.Length), System.IO.SeekOrigin.Begin);

            descriptorOffsets = WriteShapeAttributeDescriptors(writer);

            // Packet matrix index data offset
            writer.Seek((int)(start + 28), System.IO.SeekOrigin.Begin);
            writer.Write((int)(writer.BaseStream.Length - start));
            writer.Seek((int)(writer.BaseStream.Length), System.IO.SeekOrigin.Begin);

            packetMatrixOffsets = WritePacketMatrixIndices(writer);

            StreamUtility.PadStreamWithString(writer, 32);

            // Primitive data offset
            writer.Seek((int)(start + 32), System.IO.SeekOrigin.Begin);
            writer.Write((int)(writer.BaseStream.Length - start));
            writer.Seek((int)(writer.BaseStream.Length), System.IO.SeekOrigin.Begin);

            packetPrimitiveOffsets = WritePrimitives(writer);

            // Packet matrix index metadata offset
            writer.Seek((int)(start + 36), System.IO.SeekOrigin.Begin);
            writer.Write((int)(writer.BaseStream.Length - start));
            writer.Seek((int)(writer.BaseStream.Length), System.IO.SeekOrigin.Begin);

            foreach (Tuple<Packet, int> tup in packetMatrixOffsets)
            {
                writer.Write((short)0); // ???
                writer.Write((short)tup.Item1.MatrixIndices.Count);
                writer.Write(tup.Item2);
            }

            // Packet primitive metadata offset
            writer.Seek((int)(start + 40), System.IO.SeekOrigin.Begin);
            writer.Write((int)(writer.BaseStream.Length - start));
            writer.Seek((int)(writer.BaseStream.Length), System.IO.SeekOrigin.Begin);

            foreach (Tuple<int, int> tup in packetPrimitiveOffsets)
            {
                writer.Write(tup.Item1);
                writer.Write(tup.Item2);
            }

            StreamUtility.PadStreamWithString(writer, 32);

            writer.Seek((int)(start + 44), System.IO.SeekOrigin.Begin);

            foreach (Shape shape in Shapes)
            {
                writer.Seek(4, System.IO.SeekOrigin.Current);
                writer.Write((short)descriptorOffsets.Find(x => x.Item1 == shape.Descriptor).Item2);
                writer.Write((short)packetMatrixOffsets.IndexOf(packetMatrixOffsets.Find(x => x.Item1 == shape.Packets[0])));
                writer.Write((short)packetMatrixOffsets.IndexOf(packetMatrixOffsets.Find(x => x.Item1 == shape.Packets[0])));
                writer.Seek(30, System.IO.SeekOrigin.Current);
            }

            writer.Seek((int)writer.BaseStream.Length, System.IO.SeekOrigin.Begin);

            long end = writer.BaseStream.Position;
            long length = (end - start);

            writer.Seek((int)start + 4, System.IO.SeekOrigin.Begin);
            writer.Write((int)length);
            writer.Seek((int)end, System.IO.SeekOrigin.Begin);
        }

        private List<Tuple<ShapeVertexDescriptor, int>> WriteShapeAttributeDescriptors(EndianBinaryWriter writer)
        {
            List<Tuple<ShapeVertexDescriptor, int>> outList = new List<Tuple<ShapeVertexDescriptor, int>>();
            List<ShapeVertexDescriptor> written = new List<ShapeVertexDescriptor>();

            long start = writer.BaseStream.Position;

            foreach (Shape shape in Shapes)
            {
                if (written.Contains(shape.Descriptor))
                    continue;
                else
                {
                    outList.Add(new Tuple<ShapeVertexDescriptor, int>(shape.Descriptor, (int)(writer.BaseStream.Position - start)));
                    shape.Descriptor.Write(writer);
                    written.Add(shape.Descriptor);
                }
            }

            return outList;
        }

        private List<Tuple<Packet, int>> WritePacketMatrixIndices(EndianBinaryWriter writer)
        {
            List<Tuple<Packet, int>> outList = new List<Tuple<Packet, int>>();

            int indexOffset = 0;
            foreach (Shape shape in Shapes)
            {
                foreach (Packet pack in shape.Packets)
                {
                    outList.Add(new Tuple<Packet, int>(pack, indexOffset));

                    foreach (int integer in pack.MatrixIndices)
                    {
                        writer.Write((ushort)integer);
                        indexOffset++;
                    }
                }
            }

            return outList;
        }

        private List<Tuple<int, int>> WritePrimitives(EndianBinaryWriter writer)
        {
            List<Tuple<int, int>> outList = new List<Tuple<int, int>>();

            long start = writer.BaseStream.Position;

            foreach (Shape shape in Shapes)
            {
                foreach (Packet pack in shape.Packets)
                {
                    int offset = (int)(writer.BaseStream.Position - start);

                    foreach (Primitive prim in pack.Primitives)
                    {
                        prim.Write(writer, shape.Descriptor);
                    }

                    StreamUtility.PadStreamWithZero(writer, 32);

                    outList.Add(new Tuple<int, int>((int)((writer.BaseStream.Position - start) - offset), offset));
                }
            }

            return outList;
        }
        public void DumpJson(string path) {
            JsonSerializer serial = new JsonSerializer();
            serial.Formatting = Formatting.Indented;
            serial.Converters.Add(new StringEnumConverter());


            using (FileStream strm = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                StreamWriter writer = new StreamWriter(strm);
                writer.AutoFlush = true;
                serial.Serialize(writer, this);
            }
        }
    }
}
