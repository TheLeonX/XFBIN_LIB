using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using XFBIN_LIB.XFBIN;

namespace XFBIN_LIB {
    public class XFBIN_READER {
        public XFBIN.XFBIN XfbinFile = new XFBIN.XFBIN();
        public void ReadXFBIN(string path = "") {
            XfbinFile = new XFBIN.XFBIN();
            using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open))) {
                XfbinFile.MAGIC = reader.ReadChars(4);
                XfbinFile.FileID = reader.ReadUInt32BE();
                reader.BaseStream.Seek(8, SeekOrigin.Current);
                XfbinFile.ChunkTableSize = reader.ReadUInt32BE();
                XfbinFile.MinPageSize = reader.ReadUInt32BE();
                XfbinFile.FileVersion = reader.ReadUInt16BE();
                XfbinFile.FileVersionAttribute = reader.ReadUInt16BE();
                Console.WriteLine($"Version: {XfbinFile.FileVersion}  Attribute: {XfbinFile.FileVersionAttribute}");


                XfbinFile.ChunkTable.ChunkTypeCount = reader.ReadUInt32BE();
                XfbinFile.ChunkTable.ChunkTypeSize = reader.ReadUInt32BE();
                XfbinFile.ChunkTable.FilePathCount = reader.ReadUInt32BE();
                XfbinFile.ChunkTable.FilePathSize = reader.ReadUInt32BE();
                XfbinFile.ChunkTable.ChunkNameCount = reader.ReadUInt32BE();
                XfbinFile.ChunkTable.ChunkNameSize = reader.ReadUInt32BE();
                XfbinFile.ChunkTable.ChunkMapCount = reader.ReadUInt32BE();
                XfbinFile.ChunkTable.ChunkMapSize = reader.ReadUInt32BE();
                XfbinFile.ChunkTable.ChunkMapIndicesCount = reader.ReadUInt32BE();
                XfbinFile.ChunkTable.ExtraIndicesCount = reader.ReadUInt32BE();
                XfbinFile.ChunkTable.ChunkTypes = reader.ReadChunkTypeList((int)XfbinFile.ChunkTable.ChunkTypeSize);
                XfbinFile.ChunkTable.FilePaths = reader.ReadFilePathList((int)XfbinFile.ChunkTable.FilePathSize);
                XfbinFile.ChunkTable.ChunkNames = reader.ReadChunkNameList((int)XfbinFile.ChunkTable.ChunkNameSize);

                //skip 00s
                int offset = (int)reader.BaseStream.Position;
                int add_null = 4 - (offset % 4);
                reader.BaseStream.Seek(add_null, SeekOrigin.Current);

                XfbinFile.ChunkTable.ChunkMaps = reader.ReadChunkMapList((int)XfbinFile.ChunkTable.ChunkMapSize, (int)XfbinFile.ChunkTable.ChunkMapCount);
                XfbinFile.ChunkTable.ExtraMappings = reader.ReadExtraChunkMapList((int)XfbinFile.ChunkTable.ExtraIndicesCount * 8, (int)XfbinFile.ChunkTable.ExtraIndicesCount);
                XfbinFile.ChunkTable.ChunkMapIndices = reader.ReadChunkMapIndicesList((int)XfbinFile.ChunkTable.ChunkMapIndicesCount * 4, (int)XfbinFile.ChunkTable.ChunkMapIndicesCount);

                //read pages and chunks
                int extraMappingOffset = 0;
                int chunkIndicesOffset = 0;

                int page_id = 0;
                while (reader.BaseStream.Position != reader.BaseStream.Length) {
                    PAGE new_page = new PAGE();
                    while (reader.BaseStream.Position != reader.BaseStream.Length) {
                        CHUNK read_chunk = reader.ReadChunk();
                        read_chunk.ChunkMapIndex += (UInt32)chunkIndicesOffset;
                        //Console.WriteLine($"Page: {XfbinFile.ChunkTable.ChunkTypes[(int)XfbinFile.ChunkTable.ChunkMaps[(int)XfbinFile.ChunkTable.ChunkMapIndices[(int)read_chunk.ChunkMapIndex + chunkIndicesOffset].ChunkMapIndex].ChunkTypeIndex].ChunkTypeName}");
                        new_page.Chunks.Add(read_chunk);
                        if (XfbinFile.ChunkTable.ChunkTypes[(int)XfbinFile.ChunkTable.ChunkMaps[(int)XfbinFile.ChunkTable.ChunkMapIndices[(int)read_chunk.ChunkMapIndex].ChunkMapIndex].ChunkTypeIndex].ChunkTypeName == "nuccChunkPage") {

                            byte[] ExtraIndicesCountByte = new byte[4];
                            Array.Copy(read_chunk.ChunkData, 4, ExtraIndicesCountByte, 0, 4);
                            Array.Reverse(ExtraIndicesCountByte);

                            byte[] PageSizeByte = new byte[4];
                            Array.Copy(read_chunk.ChunkData, 0, PageSizeByte, 0, 4);
                            Array.Reverse(PageSizeByte);

                            UInt32 ExtraIndicesCount = BitConverter.ToUInt32(ExtraIndicesCountByte);
                            UInt32 PageSize = BitConverter.ToUInt32(PageSizeByte);

                            for (int i = 0 + chunkIndicesOffset; i < chunkIndicesOffset + PageSize; i++) {

                                new_page.ChunkMappings.Add(XfbinFile.ChunkTable.ChunkMaps[(int)XfbinFile.ChunkTable.ChunkMapIndices[i].ChunkMapIndex]);
                            }

                            for (int i = 0 + extraMappingOffset; i < extraMappingOffset + ExtraIndicesCount; i++) {

                                new_page.ExtraMappings.Add(XfbinFile.ChunkTable.ExtraMappings[i]);
                            }
                            extraMappingOffset += (int)ExtraIndicesCount;
                            chunkIndicesOffset += (int)PageSize;
                            break;
                        }
                    }
                    // optional, needed just for names
                    if (XfbinFile.ChunkTable.ChunkTypes[(int)new_page.ChunkMappings[0].ChunkTypeIndex].ChunkTypeName != "nuccChunkNull") {
                        for (int i = new_page.ChunkMappings.Count - 1; i > 0 ; i--) {
                            string type = XfbinFile.ChunkTable.ChunkTypes[(int)new_page.ChunkMappings[i].ChunkTypeIndex].ChunkTypeName;
                            if (type == "nuccChunkAnm" ||
                                type == "nuccChunkAnmStrm" ||
                                type == "nuccChunkClump" ||
                                type == "nuccChunkStrmAnm" ||
                                type == "nuccChunkParticle" ||
                                type == "nuccChunkTexture" ||
                                type == "nuccChunkBinary" ||
                                type == "nuccChunkTrail" ||
                                type == "nuccChunkDynamics" ||
                                type == "nuccChunkSprite" ||
                                type == "nuccChunkSpriteAnm"
                                ) {
                                new_page.PageName = "[" + page_id.ToString("D3") + "] " + XfbinFile.ChunkTable.ChunkNames[(int)new_page.ChunkMappings[i].ChunkNameIndex].ChunkName + " (" + type + ")";
                                break;
                            }
                        }
                    } else {
                        for (int i = 0; i < new_page.ChunkMappings.Count; i++) {
                            string type = XfbinFile.ChunkTable.ChunkTypes[(int)new_page.ChunkMappings[i].ChunkTypeIndex].ChunkTypeName;
                            if (type == "nuccChunkAnm" ||
                                type == "nuccChunkAnmStrm" ||
                                type == "nuccChunkClump" ||
                                type == "nuccChunkStrmAnm" ||
                                type == "nuccChunkParticle" ||
                                type == "nuccChunkTexture" ||
                                type == "nuccChunkBinary" ||
                                type == "nuccChunkTrail" ||
                                type == "nuccChunkDynamics" ||
                                type == "nuccChunkSprite" ||
                                type == "nuccChunkSpriteAnm"
                                ) {
                                new_page.PageName = "[" + page_id.ToString("D3") + "] " + XfbinFile.ChunkTable.ChunkNames[(int)new_page.ChunkMappings[i].ChunkNameIndex].ChunkName + " (" + type + ")";
                                break;
                            }
                        }
                    }

                    new_page.ChunkTable = XfbinFile.ChunkTable;
                    XfbinFile.Pages.Add(new_page);
                    page_id++;
                }
                for (int i = 0; i < XfbinFile.Pages.Count; i++) {
                    Console.WriteLine($"Page: {XfbinFile.Pages[i].PageName}");
                }
                //for (int i = 0; i<XfbinFile.ChunkTable.ChunkMapCount; i++) {
                //    Console.WriteLine($"Type: {XfbinFile.ChunkTable.ChunkTypes[(int)XfbinFile.ChunkTable.ChunkMaps[i].ChunkTypeIndex].ChunkTypeName}" +
                //    $" Path: {XfbinFile.ChunkTable.FilePaths[(int)XfbinFile.ChunkTable.ChunkMaps[i].FilePathIndex].FilePathName}" +
                //    $" Name: {XfbinFile.ChunkTable.ChunkNames[(int)XfbinFile.ChunkTable.ChunkMaps[i].ChunkNameIndex].ChunkName}");
                //}
                //for (int i = 0; i < XfbinFile.ChunkTable.ExtraIndicesCount; i++) {
                //    Console.WriteLine($"{XfbinFile.ChunkTable.ChunkNames[(int)XfbinFile.ChunkTable.ExtraMappings[i].ChunkNameIndex].ChunkName}" +
                //    $" [{XfbinFile.ChunkTable.ChunkNames[(int)XfbinFile.ChunkTable.ChunkMaps[(int)XfbinFile.ChunkTable.ExtraMappings[i].ChunkMapIndex].ChunkNameIndex].ChunkName}" +
                //    $" ({XfbinFile.ChunkTable.ChunkTypes[(int)XfbinFile.ChunkTable.ChunkMaps[(int)XfbinFile.ChunkTable.ExtraMappings[i].ChunkMapIndex].ChunkTypeIndex].ChunkTypeName})]");
                //}
            }
        }

        public string GetXfbinChunkType(int ChunkMapIndex)
        {
            int index = (int)XfbinFile.ChunkTable.ChunkMapIndices[ChunkMapIndex].ChunkMapIndex;


            return XfbinFile.ChunkTable.ChunkTypes[(int)XfbinFile.ChunkTable.ChunkMaps[index].ChunkTypeIndex].ChunkTypeName;
        }
    }
    public static class Helpers {
        // Note this MODIFIES THE GIVEN ARRAY then returns a reference to the modified array.
        public static byte[] Reverse(this byte[] b) {
            Array.Reverse(b);
            return b;
        }

        public static ObservableCollection<CHUNK_TYPE> ReadChunkTypeList(this BinaryReader binRdr, int size) {
            ObservableCollection<CHUNK_TYPE> return_list = new ObservableCollection<CHUNK_TYPE>();
            byte[] byteTypes = binRdr.ReadBytes(size-1);
            string[] readChunkTypesString = Encoding.ASCII.GetString(byteTypes).Split(new char[] { '\0' });
            for (int i = 0; i < readChunkTypesString.Length; i++) {
                return_list.Add(new CHUNK_TYPE { ChunkTypeName = readChunkTypesString[i] });
            }
            binRdr.BaseStream.Seek(1, SeekOrigin.Current);
            return return_list;
        }
        public static ObservableCollection<FILE_PATH> ReadFilePathList(this BinaryReader binRdr, int size) {
            ObservableCollection<FILE_PATH> return_list = new ObservableCollection<FILE_PATH>();
            byte[] byteTypes = binRdr.ReadBytes(size -1);
            string[] readChunkTypesString = Encoding.ASCII.GetString(byteTypes).Split(new char[] { '\0' });
            for (int i = 0; i < readChunkTypesString.Length; i++) {
                return_list.Add(new FILE_PATH { FilePathName = readChunkTypesString[i] });
            }
            binRdr.BaseStream.Seek(1, SeekOrigin.Current);
            return return_list;
        }
        public static ObservableCollection<CHUNK_NAME> ReadChunkNameList(this BinaryReader binRdr, int size) {
            ObservableCollection<CHUNK_NAME> return_list = new ObservableCollection<CHUNK_NAME>();
            byte[] byteTypes = binRdr.ReadBytes(size - 1);
            string[] readChunkTypesString = Encoding.ASCII.GetString(byteTypes).Split(new char[] { '\0' });
            for (int i = 0; i < readChunkTypesString.Length; i++) {
                return_list.Add(new CHUNK_NAME { ChunkName = readChunkTypesString[i] });
            }
            return return_list;
        }
        public static ObservableCollection<CHUNK_MAP> ReadChunkMapList(this BinaryReader binRdr, int size, int count) {
            ObservableCollection<CHUNK_MAP> return_list = new ObservableCollection<CHUNK_MAP>();
            byte[] byteTypes = binRdr.ReadBytes(size);
            for (int i = 0; i < count; i++) {
                byte[] ChunkType = new byte[4];
                Array.Copy(byteTypes, i * 0xC, ChunkType, 0, 4);
                Array.Reverse(ChunkType);
                byte[] FilePath = new byte[4];
                Array.Copy(byteTypes, (i * 0xC) + 0x04, FilePath, 0, 4);
                Array.Reverse(FilePath);
                byte[] ChunkName = new byte[4];
                Array.Copy(byteTypes, (i * 0xC) + 0x08, ChunkName, 0, 4);
                Array.Reverse(ChunkName);
                return_list.Add(new CHUNK_MAP {
                    ChunkTypeIndex = BitConverter.ToUInt32(ChunkType),
                    FilePathIndex = BitConverter.ToUInt32(FilePath),
                    ChunkNameIndex = BitConverter.ToUInt32(ChunkName)
                });
            }
            return return_list;
        }
        public static ObservableCollection<EXTRA_CHUNK_MAP_INDICES> ReadExtraChunkMapList(this BinaryReader binRdr, int size, int count, int offset = 0) {
            ObservableCollection<EXTRA_CHUNK_MAP_INDICES> return_list = new ObservableCollection<EXTRA_CHUNK_MAP_INDICES>();
            byte[] byteTypes = binRdr.ReadBytes(size);
            for (int i = 0 + offset; i < offset+count; i++) {
                byte[] ChunkName = new byte[4];
                Array.Copy(byteTypes, i * 0x8, ChunkName, 0, 4);
                Array.Reverse(ChunkName);
                byte[] ChunkMap = new byte[4];
                Array.Copy(byteTypes, (i * 0x8) + 0x04, ChunkMap, 0, 4);
                Array.Reverse(ChunkMap);

                return_list.Add(new EXTRA_CHUNK_MAP_INDICES {
                    ChunkNameIndex = BitConverter.ToUInt32(ChunkName),
                    ChunkMapIndex = BitConverter.ToUInt32(ChunkMap)
                });
            }
            return return_list;
        }
        public static ObservableCollection<CHUNK_MAP_INDICES> ReadChunkMapIndicesList(this BinaryReader binRdr, int size, int count) {
            ObservableCollection<CHUNK_MAP_INDICES> return_list = new ObservableCollection<CHUNK_MAP_INDICES>();
            byte[] byteTypes = binRdr.ReadBytes(size);
            for (int i = 0; i < count; i++) {
                byte[] ChunkMap = new byte[4];
                Array.Copy(byteTypes, i * 0x4, ChunkMap, 0, 4);
                Array.Reverse(ChunkMap);

                return_list.Add(new CHUNK_MAP_INDICES {
                    ChunkMapIndex = BitConverter.ToUInt32(ChunkMap)
                });
            }
            return return_list;
        }

        public static CHUNK ReadChunk(this BinaryReader binRdr) {
            byte[] byteTypes = binRdr.ReadBytes(12);
            byte[] size = new byte[4];
            Array.Copy(byteTypes, 0, size, 0, 4);
            Array.Reverse(size);
            byte[] ChunkMapIndex = new byte[4];
            Array.Copy(byteTypes, 4, ChunkMapIndex, 0, 4);
            Array.Reverse(ChunkMapIndex);
            byte[] Version = new byte[2];
            Array.Copy(byteTypes, 8, Version, 0, 2);
            Array.Reverse(Version);
            byte[] VersionAttribute = new byte[2];
            Array.Copy(byteTypes, 10, VersionAttribute, 0, 2);
            Array.Reverse(VersionAttribute);
            byte[] ChunkData = binRdr.ReadBytes(BitConverter.ToInt32(size));

            return new CHUNK {
                Size = BitConverter.ToUInt32(size),
                ChunkMapIndex = BitConverter.ToUInt32(ChunkMapIndex),
                Version = BitConverter.ToUInt16(Version),
                VersionAttribute = BitConverter.ToUInt16(VersionAttribute),
                ChunkData = ChunkData
            };
        }

        public static UInt16 ReadUInt16BE(this BinaryReader binRdr) {
            return BitConverter.ToUInt16(binRdr.ReadBytesRequired(sizeof(UInt16)).Reverse(), 0);
        }

        public static Int16 ReadInt16BE(this BinaryReader binRdr) {
            return BitConverter.ToInt16(binRdr.ReadBytesRequired(sizeof(Int16)).Reverse(), 0);
        }

        public static UInt32 ReadUInt32BE(this BinaryReader binRdr) {
            return BitConverter.ToUInt32(binRdr.ReadBytesRequired(sizeof(UInt32)).Reverse(), 0);
        }

        public static Int32 ReadInt32BE(this BinaryReader binRdr) {
            return BitConverter.ToInt32(binRdr.ReadBytesRequired(sizeof(Int32)).Reverse(), 0);
        }




        public static byte[] ReadBytesRequired(this BinaryReader binRdr, int byteCount) {
            var result = binRdr.ReadBytes(byteCount);

            if (result.Length != byteCount)
                throw new EndOfStreamException(string.Format("{0} bytes required from stream, but only {1} returned.", byteCount, result.Length));

            return result;
        }
    }
}
