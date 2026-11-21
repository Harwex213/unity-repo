using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hexwex.Core;

namespace Hexwex.Net
{
    /// <summary>What a message is. The first byte of every message.</summary>
    public enum MessageKind : byte
    {
        /// <summary>Client to host: the protocol it speaks and the name it plays under.</summary>
        Hello = 1,
        /// <summary>Host to one client: who is at the table, the world seed, and which seat is theirs.</summary>
        Lobby = 2,
        /// <summary>Host to one client: the game has begun, with the seats in their final order.</summary>
        Start = 3,
        /// <summary>Client to host: an action the client wants to take.</summary>
        Submit = 4,
        /// <summary>Host to everyone: an action in its place in the game's order.</summary>
        Command = 5,
        /// <summary>Client to host: the checksum of its game after a numbered command.</summary>
        Checksum = 6,
        /// <summary>Host to one client: it cannot join, and why.</summary>
        Refused = 7,
    }

    /// <summary>One message, decoded. Only the fields of its kind are set.</summary>
    public sealed class Message
    {
        public MessageKind Kind;
        public int Protocol;
        public string Name;
        public string Seed;
        public List<string> Names = new List<string>();
        /// <summary>The seat of the client the message is sent to, for a lobby or a start.</summary>
        public int Seat = -1;
        public int Sequence;
        public NetCommand Command;
        public uint Hash;
        public string Reason;
    }

    /// <summary>
    /// The byte form of the messages. Little-endian, strings as UTF-8 with a
    /// length in front, lists with a count in front. Nothing here depends on
    /// the engine, so a host can run anywhere C# runs.
    /// </summary>
    public static class Wire
    {
        /// <summary>Raised whenever the byte form changes, so an old client is turned away rather than misread.</summary>
        public const int Protocol = 1;

        public static byte[] Hello(string name) => Encode(writer => { writer.Write((byte)MessageKind.Hello); writer.Write(Protocol); Text(writer, name); });

        public static byte[] Lobby(string seed, IReadOnlyList<string> names, int seat) => Encode(writer =>
        {
            writer.Write((byte)MessageKind.Lobby);
            Text(writer, seed);
            Texts(writer, names);
            writer.Write(seat);
        });

        public static byte[] Start(string seed, IReadOnlyList<string> names, int seat) => Encode(writer =>
        {
            writer.Write((byte)MessageKind.Start);
            Text(writer, seed);
            Texts(writer, names);
            writer.Write(seat);
        });

        public static byte[] Submit(NetCommand command) => Encode(writer => { writer.Write((byte)MessageKind.Submit); WriteCommand(writer, command); });

        public static byte[] Command(int sequence, NetCommand command) => Encode(writer =>
        {
            writer.Write((byte)MessageKind.Command);
            writer.Write(sequence);
            WriteCommand(writer, command);
        });

        public static byte[] Checksum(int sequence, uint hash) => Encode(writer => { writer.Write((byte)MessageKind.Checksum); writer.Write(sequence); writer.Write(hash); });

        public static byte[] Refused(string reason) => Encode(writer => { writer.Write((byte)MessageKind.Refused); Text(writer, reason); });

        /// <summary>Reads a message. Throws <see cref="InvalidDataException"/> on bytes that are not one.</summary>
        public static Message Decode(byte[] data)
        {
            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(data), Encoding.UTF8))
                {
                    Message message = new Message { Kind = (MessageKind)reader.ReadByte() };
                    switch (message.Kind)
                    {
                        case MessageKind.Hello:
                            message.Protocol = reader.ReadInt32();
                            message.Name = ReadText(reader);
                            break;
                        case MessageKind.Lobby:
                        case MessageKind.Start:
                            message.Seed = ReadText(reader);
                            message.Names = ReadTexts(reader);
                            message.Seat = reader.ReadInt32();
                            break;
                        case MessageKind.Submit:
                            message.Command = ReadCommand(reader);
                            break;
                        case MessageKind.Command:
                            message.Sequence = reader.ReadInt32();
                            message.Command = ReadCommand(reader);
                            break;
                        case MessageKind.Checksum:
                            message.Sequence = reader.ReadInt32();
                            message.Hash = reader.ReadUInt32();
                            break;
                        case MessageKind.Refused:
                            message.Reason = ReadText(reader);
                            break;
                        default:
                            throw new InvalidDataException("Unknown message " + (int)message.Kind);
                    }

                    return message;
                }
            }
            catch (EndOfStreamException exception)
            {
                throw new InvalidDataException("Message cut short", exception);
            }
            catch (ArgumentException exception)
            {
                // An unknown unit, or bytes that are not text.
                throw new InvalidDataException("Message holds an unknown value", exception);
            }
        }

        private static byte[] Encode(Action<BinaryWriter> write)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    write(writer);
                }

                return stream.ToArray();
            }
        }

        private static void WriteCommand(BinaryWriter writer, NetCommand command)
        {
            writer.Write((byte)command.Kind);
            Text(writer, command.PlayerId);
            Text(writer, command.Target);
            Text(writer, command.Second);
            writer.Write(command.Value);
            writer.Write(command.Result != null);
            if (command.Result != null)
            {
                WriteResult(writer, command.Result);
            }
        }

        private static NetCommand ReadCommand(BinaryReader reader)
        {
            NetCommand command = new NetCommand
            {
                Kind = (CommandKind)reader.ReadByte(),
                PlayerId = ReadText(reader),
                Target = ReadText(reader),
                Second = ReadText(reader),
                Value = reader.ReadInt32(),
            };
            if (reader.ReadBoolean())
            {
                command.Result = ReadResult(reader);
            }

            return command;
        }

        private static void WriteResult(BinaryWriter writer, CleanupResult result)
        {
            writer.Write((byte)result.Outcome);
            writer.Write(result.TotalIslands);
            writer.Write(result.ClearedIslands);
            writer.Write(result.AttachedIslands);
            writer.Write(result.Kills);
            writer.Write(result.Razed);
            writer.Write(result.ManaSpent);

            List<AnnexedHex> annexed = result.Annexed ?? new List<AnnexedHex>();
            writer.Write(annexed.Count);
            foreach (AnnexedHex hex in annexed)
            {
                writer.Write(hex.Q);
                writer.Write(hex.R);
                writer.Write((byte)hex.Biome);
                writer.Write(hex.Toxicity);
            }

            Units(writer, result.Survivors);
            Units(writer, result.Lost);

            List<StructureResult> structures = result.Structures ?? new List<StructureResult>();
            writer.Write(structures.Count);
            foreach (StructureResult structure in structures)
            {
                Text(writer, structure.HexId);
                writer.Write(structure.Hp);
                writer.Write(structure.StartHp);
                writer.Write(structure.MaxHp);
            }

            Texts(writer, result.DestroyedHexIds ?? new List<string>());
        }

        private static CleanupResult ReadResult(BinaryReader reader)
        {
            CleanupResult result = new CleanupResult
            {
                Outcome = (CleanupOutcome)reader.ReadByte(),
                TotalIslands = reader.ReadInt32(),
                ClearedIslands = reader.ReadInt32(),
                AttachedIslands = reader.ReadInt32(),
                Kills = reader.ReadInt32(),
                Razed = reader.ReadInt32(),
                ManaSpent = reader.ReadInt32(),
                Annexed = new List<AnnexedHex>(),
                Structures = new List<StructureResult>(),
            };

            int annexed = Count(reader);
            for (int index = 0; index < annexed; index += 1)
            {
                result.Annexed.Add(new AnnexedHex { Q = reader.ReadInt32(), R = reader.ReadInt32(), Biome = (BiomeId)reader.ReadByte(), Toxicity = reader.ReadInt32() });
            }

            result.Survivors = ReadUnits(reader);
            result.Lost = ReadUnits(reader);

            int structures = Count(reader);
            for (int index = 0; index < structures; index += 1)
            {
                result.Structures.Add(new StructureResult { HexId = ReadText(reader), Hp = reader.ReadInt32(), StartHp = reader.ReadInt32(), MaxHp = reader.ReadInt32() });
            }

            result.DestroyedHexIds = ReadTexts(reader);

            return result;
        }

        /// <summary>Units go by their key: every machine has the same roster.</summary>
        private static void Units(BinaryWriter writer, List<Unit> units)
        {
            List<string> keys = new List<string>();
            foreach (Unit unit in units ?? new List<Unit>())
            {
                keys.Add(unit.Key);
            }

            Texts(writer, keys);
        }

        private static List<Unit> ReadUnits(BinaryReader reader)
        {
            List<Unit> units = new List<Unit>();
            foreach (string key in ReadTexts(reader))
            {
                units.Add(Hexwex.Core.Units.Get(key));
            }

            return units;
        }

        /// <summary>A string, or <c>null</c>: a length of -1 stands for null.</summary>
        private static void Text(BinaryWriter writer, string text)
        {
            if (text == null)
            {
                writer.Write(-1);

                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadText(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0)
            {
                return null;
            }

            if (length > reader.BaseStream.Length - reader.BaseStream.Position)
            {
                throw new InvalidDataException("String longer than the message");
            }

            return Encoding.UTF8.GetString(reader.ReadBytes(length));
        }

        private static void Texts(BinaryWriter writer, IReadOnlyList<string> texts)
        {
            writer.Write(texts.Count);
            foreach (string text in texts)
            {
                Text(writer, text);
            }
        }

        private static List<string> ReadTexts(BinaryReader reader)
        {
            int count = Count(reader);
            List<string> texts = new List<string>(count);
            for (int index = 0; index < count; index += 1)
            {
                texts.Add(ReadText(reader));
            }

            return texts;
        }

        /// <summary>A list's length, checked against what is left of the message so a bad count cannot ask for gigabytes.</summary>
        private static int Count(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > reader.BaseStream.Length - reader.BaseStream.Position)
            {
                throw new InvalidDataException("Bad list length " + count);
            }

            return count;
        }
    }
}
