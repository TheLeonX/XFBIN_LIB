using System.Collections.ObjectModel;
using System.Text;
using XFBIN_LIB.XFBIN;

namespace XFBIN_LIB
{
    public class XFBIN_READER
    {
        public XFBIN.XFBIN XfbinFile = new XFBIN.XFBIN();
        public void ReadXFBIN(string path = "")
        {
            XfbinFile = new XFBIN.XFBIN();
            using (var reader = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                // Read header
                XfbinFile.MAGIC = reader.ReadChars(4);
                XfbinFile.FileID = reader.ReadUInt32BE();
                reader.BaseStream.Seek(8, SeekOrigin.Current);
                XfbinFile.ChunkTableSize = reader.ReadUInt32BE();
                XfbinFile.MinPageSize = reader.ReadUInt32BE();
                XfbinFile.FileVersion = reader.ReadUInt16BE();
                XfbinFile.FileVersionAttribute = reader.ReadUInt16BE();
                Console.WriteLine($"Version: {XfbinFile.FileVersion}  Attribute: {XfbinFile.FileVersionAttribute}");

                // Read ChunkTable header values
                var table = XfbinFile.ChunkTable;
                table.ChunkTypeCount = reader.ReadUInt32BE();
                table.ChunkTypeSize = reader.ReadUInt32BE();
                table.FilePathCount = reader.ReadUInt32BE();
                table.FilePathSize = reader.ReadUInt32BE();
                table.ChunkNameCount = reader.ReadUInt32BE();
                table.ChunkNameSize = reader.ReadUInt32BE();
                table.ChunkMapCount = reader.ReadUInt32BE();
                table.ChunkMapSize = reader.ReadUInt32BE();
                table.ChunkMapIndicesCount = reader.ReadUInt32BE();
                table.ExtraIndicesCount = reader.ReadUInt32BE();

                // Read ChunkTable lists
                table.ChunkTypes = reader.ReadChunkTypeList((int)table.ChunkTypeSize);
                table.FilePaths = reader.ReadFilePathList((int)table.FilePathSize);
                table.ChunkNames = reader.ReadChunkNameList((int)table.ChunkNameSize);

                // Align stream to 4-byte boundary
                int offset = (int)reader.BaseStream.Position;
                int alignment = (4 - (offset % 4)) % 4;
                reader.BaseStream.Seek(alignment, SeekOrigin.Current);

                // Read remaining ChunkTable lists
                table.ChunkMaps = reader.ReadChunkMapList((int)table.ChunkMapSize, (int)table.ChunkMapCount);
                table.ExtraMappings = reader.ReadExtraChunkMapList((int)table.ExtraIndicesCount * 8, (int)table.ExtraIndicesCount);
                table.ChunkMapIndices = reader.ReadChunkMapIndicesList((int)table.ChunkMapIndicesCount * 4, (int)table.ChunkMapIndicesCount);

                int extraMappingOffset = 0;
                int chunkIndicesOffset = 0;
                int pageId = 0;

                // Process pages and their chunks
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    PAGE newPage = new PAGE();
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        CHUNK chunk = reader.ReadChunk();
                        chunk.ChunkMapIndex += (UInt32)chunkIndicesOffset;
                        newPage.Chunks.Add(chunk);

                        // Detect page break when encountering "nuccChunkPage"
                        var chunkMapIndex = (int)chunk.ChunkMapIndex;
                        var mapIndex = (int)table.ChunkMapIndices[chunkMapIndex].ChunkMapIndex;
                        string chunkType = table.ChunkTypes[(int)table.ChunkMaps[mapIndex].ChunkTypeIndex].ChunkTypeName;
                        if (chunkType == "nuccChunkPage")
                        {
                            UInt32 pageSize = ReadUInt32BigEndian(chunk.ChunkData, 0);
                            UInt32 extraCount = ReadUInt32BigEndian(chunk.ChunkData, 4);

                            for (int i = chunkIndicesOffset; i < chunkIndicesOffset + pageSize; i++)
                            {
                                newPage.ChunkMappings.Add(table.ChunkMaps[(int)table.ChunkMapIndices[i].ChunkMapIndex]);
                            }
                            for (int i = extraMappingOffset; i < extraMappingOffset + extraCount; i++)
                            {
                                newPage.ExtraMappings.Add(table.ExtraMappings[i]);
                            }
                            extraMappingOffset += (int)extraCount;
                            chunkIndicesOffset += (int)pageSize;
                            break;
                        }
                    }

                    // Set page name based on valid chunk types
                    newPage.PageName = GetPageName(newPage, pageId, table);
                    newPage.ChunkTable = table;
                    XfbinFile.Pages.Add(newPage);
                    pageId++;
                }

                foreach (var page in XfbinFile.Pages)
                {
                    Console.WriteLine($"Page: {page.PageName}");
                }
            }
        }

        /// <summary>
        /// Reads a big-endian UInt32 from the byte array starting at the given offset.
        /// </summary>
        private UInt32 ReadUInt32BigEndian(byte[] data, int offset)
        {
            byte[] bytes = new byte[4];
            Array.Copy(data, offset, bytes, 0, 4);
            Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        /// <summary>
        /// Returns the page name based on valid chunk types.
        /// </summary>
        private string GetPageName(PAGE page, int pageId, CHUNK_TABLE table)
        {
            string[] validTypes = {
        "nuccChunkAnm", "nuccChunkAnmStrm", "nuccChunkClump", "nuccChunkStrmAnm",
        "nuccChunkParticle", "nuccChunkTexture", "nuccChunkBinary", "nuccChunkTrail",
        "nuccChunkDynamics", "nuccChunkSprite", "nuccChunkSpriteAnm"
    };

            bool useReverse = table.ChunkTypes[(int)table.ChunkMaps[(int)page.ChunkMappings[0].ChunkTypeIndex].ChunkTypeIndex].ChunkTypeName != "nuccChunkNull";
            int start = useReverse ? page.Chunks.Count - 1 : 0;
            int end = useReverse ? -1 : page.Chunks.Count;
            int step = useReverse ? -1 : 1;

            for (int i = start; i != end; i += step)
            {
                int mapIdx = (int)page.Chunks[i].ChunkMapIndex;
                int actualMapIdx = (int)table.ChunkMapIndices[mapIdx].ChunkMapIndex;
                int nameIndex = (int)table.ChunkMaps[actualMapIdx].ChunkNameIndex;
                int typeIndex = (int)table.ChunkMaps[actualMapIdx].ChunkTypeIndex;
                string typeName = table.ChunkTypes[typeIndex].ChunkTypeName;
                if (validTypes.Contains(typeName))
                {
                    return $"[{pageId.ToString("D3")}] {table.ChunkNames[nameIndex].ChunkName} ({typeName})";
                }
            }
            return $"[{pageId.ToString("D3")}] Unknown";
        }


        public string GetXfbinChunkType(int ChunkMapIndex)
        {
            int index = (int)XfbinFile.ChunkTable.ChunkMapIndices[ChunkMapIndex].ChunkMapIndex;


            return XfbinFile.ChunkTable.ChunkTypes[(int)XfbinFile.ChunkTable.ChunkMaps[index].ChunkTypeIndex].ChunkTypeName;
        }
    }
    public static class Helpers
    {
        // Note this MODIFIES THE GIVEN ARRAY then returns a reference to the modified array.
        public static byte[] Reverse(this byte[] b)
        {
            Array.Reverse(b);
            return b;
        }

        public static ObservableCollection<CHUNK_TYPE> ReadChunkTypeList(this BinaryReader binRdr, int size)
        {
            ObservableCollection<CHUNK_TYPE> return_list = new ObservableCollection<CHUNK_TYPE>();
            byte[] byteTypes = binRdr.ReadBytes(size - 1);
            string[] readChunkTypesString = Encoding.UTF8.GetString(byteTypes).Split(new char[] { '\0' });
            for (int i = 0; i < readChunkTypesString.Length; i++)
            {
                return_list.Add(new CHUNK_TYPE { ChunkTypeName = readChunkTypesString[i] });
            }
            binRdr.BaseStream.Seek(1, SeekOrigin.Current);
            return return_list;
        }
        public static ObservableCollection<FILE_PATH> ReadFilePathList(this BinaryReader binRdr, int size)
        {
            ObservableCollection<FILE_PATH> return_list = new ObservableCollection<FILE_PATH>();
            byte[] byteTypes = binRdr.ReadBytes(size - 1);
            string[] readChunkTypesString = Encoding.UTF8.GetString(byteTypes).Split(new char[] { '\0' });
            for (int i = 0; i < readChunkTypesString.Length; i++)
            {
                return_list.Add(new FILE_PATH { FilePathName = readChunkTypesString[i] });
            }
            binRdr.BaseStream.Seek(1, SeekOrigin.Current);
            return return_list;
        }
        public static ObservableCollection<CHUNK_NAME> ReadChunkNameList(this BinaryReader binRdr, int size)
        {
            ObservableCollection<CHUNK_NAME> return_list = new ObservableCollection<CHUNK_NAME>();
            byte[] byteTypes = binRdr.ReadBytes(size - 1);
            string[] readChunkTypesString = Encoding.UTF8.GetString(byteTypes).Split(new char[] { '\0' });
            for (int i = 0; i < readChunkTypesString.Length; i++)
            {
                return_list.Add(new CHUNK_NAME { ChunkName = readChunkTypesString[i] });
            }
            return return_list;
        }
        public static ObservableCollection<CHUNK_MAP> ReadChunkMapList(this BinaryReader binRdr, int size, int count)
        {
            ObservableCollection<CHUNK_MAP> return_list = new ObservableCollection<CHUNK_MAP>();
            byte[] byteTypes = binRdr.ReadBytes(size);
            for (int i = 0; i < count; i++)
            {
                byte[] ChunkType = new byte[4];
                Array.Copy(byteTypes, i * 0xC, ChunkType, 0, 4);
                Array.Reverse(ChunkType);
                byte[] FilePath = new byte[4];
                Array.Copy(byteTypes, (i * 0xC) + 0x04, FilePath, 0, 4);
                Array.Reverse(FilePath);
                byte[] ChunkName = new byte[4];
                Array.Copy(byteTypes, (i * 0xC) + 0x08, ChunkName, 0, 4);
                Array.Reverse(ChunkName);
                return_list.Add(new CHUNK_MAP
                {
                    ChunkTypeIndex = BitConverter.ToUInt32(ChunkType),
                    FilePathIndex = BitConverter.ToUInt32(FilePath),
                    ChunkNameIndex = BitConverter.ToUInt32(ChunkName)
                });
            }
            return return_list;
        }
        public static ObservableCollection<EXTRA_CHUNK_MAP_INDICES> ReadExtraChunkMapList(this BinaryReader binRdr, int size, int count, int offset = 0)
        {
            ObservableCollection<EXTRA_CHUNK_MAP_INDICES> return_list = new ObservableCollection<EXTRA_CHUNK_MAP_INDICES>();
            byte[] byteTypes = binRdr.ReadBytes(size);
            for (int i = 0 + offset; i < offset + count; i++)
            {
                byte[] ChunkName = new byte[4];
                Array.Copy(byteTypes, i * 0x8, ChunkName, 0, 4);
                Array.Reverse(ChunkName);
                byte[] ChunkMap = new byte[4];
                Array.Copy(byteTypes, (i * 0x8) + 0x04, ChunkMap, 0, 4);
                Array.Reverse(ChunkMap);

                return_list.Add(new EXTRA_CHUNK_MAP_INDICES
                {
                    ChunkNameIndex = BitConverter.ToUInt32(ChunkName),
                    ChunkMapIndex = BitConverter.ToUInt32(ChunkMap)
                });
            }
            return return_list;
        }
        public static ObservableCollection<CHUNK_MAP_INDICES> ReadChunkMapIndicesList(this BinaryReader binRdr, int size, int count)
        {
            ObservableCollection<CHUNK_MAP_INDICES> return_list = new ObservableCollection<CHUNK_MAP_INDICES>();
            byte[] byteTypes = binRdr.ReadBytes(size);
            for (int i = 0; i < count; i++)
            {
                byte[] ChunkMap = new byte[4];
                Array.Copy(byteTypes, i * 0x4, ChunkMap, 0, 4);
                Array.Reverse(ChunkMap);

                return_list.Add(new CHUNK_MAP_INDICES
                {
                    ChunkMapIndex = BitConverter.ToUInt32(ChunkMap)
                });
            }
            return return_list;
        }

        public static CHUNK ReadChunk(this BinaryReader binRdr)
        {
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

            return new CHUNK
            {
                Size = BitConverter.ToUInt32(size),
                ChunkMapIndex = BitConverter.ToUInt32(ChunkMapIndex),
                Version = BitConverter.ToUInt16(Version),
                VersionAttribute = BitConverter.ToUInt16(VersionAttribute),
                ChunkData = ChunkData
            };
        }

        public static UInt16 ReadUInt16BE(this BinaryReader binRdr)
        {
            return BitConverter.ToUInt16(binRdr.ReadBytesRequired(sizeof(UInt16)).Reverse(), 0);
        }

        public static Int16 ReadInt16BE(this BinaryReader binRdr)
        {
            return BitConverter.ToInt16(binRdr.ReadBytesRequired(sizeof(Int16)).Reverse(), 0);
        }

        public static UInt32 ReadUInt32BE(this BinaryReader binRdr)
        {
            return BitConverter.ToUInt32(binRdr.ReadBytesRequired(sizeof(UInt32)).Reverse(), 0);
        }

        public static Int32 ReadInt32BE(this BinaryReader binRdr)
        {
            return BitConverter.ToInt32(binRdr.ReadBytesRequired(sizeof(Int32)).Reverse(), 0);
        }




        public static byte[] ReadBytesRequired(this BinaryReader binRdr, int byteCount)
        {
            var result = binRdr.ReadBytes(byteCount);

            if (result.Length != byteCount)
                throw new EndOfStreamException(string.Format("{0} bytes required from stream, but only {1} returned.", byteCount, result.Length));

            return result;
        }
    }
}