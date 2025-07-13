using System;
using System.IO;

namespace SuperBMDLib
{
    /// <summary>
    /// Container for arguments taken from the user's input.
    /// </summary>
    public struct Arguments
    {
        public string input_path;
        public string output_path;
        public string materials_path;
        public string output_materials_path;
        public string texheaders_path;
        public string tristrip_mode;
        public bool rotate_model;
        public bool output_bdl;
        public bool do_profile;
        public bool sort_meshes;
        public bool sort_strict;
        public bool ensure_one_material_per_mesh;
        public bool export_obj;
        public bool forceFloat;
        public bool degenerateTriangles;
        public bool readMipmaps;
        public bool dumpHierarchy;
        public string hierarchyPath;
        public bool exportAnims;
        public Geometry.Enums.GXDataType vertextype;
        public byte fraction;
        public bool material_order_strict;
        public bool export_skeleton_root;
        public string skeleton_root_marker;
        public string skeleton_root_name;
        public bool skeleton_autodetect;
        public bool include_normals;
        public string material_folder;
        public string texture_path;
        public string output_material_folder;
        public bool file_name_as_mat_name;
        public byte texfraction;
        public Scenegraph.Enums.TransformMode transform_mode;
        public bool add_envtex_attribute;
        public bool flip_faces;

        /// <summary>
        /// Initializes a new Arguments instance from the arguments passed in to SuperBMD.
        /// </summary>
        /// <param name="args">Arguments from the user</param>
        public Arguments(string[] args)
        {
            input_path = "";
            output_path = "";
            materials_path = "";
            output_materials_path = "";
            texheaders_path = "";
            tristrip_mode = "static";
            rotate_model = false;
            output_bdl = false;
            do_profile = false;
            sort_meshes = true;
            sort_strict = false;
            ensure_one_material_per_mesh = false;
            export_obj = false;
            forceFloat = false;
            degenerateTriangles = false;
            readMipmaps = true;
            dumpHierarchy = false;
            hierarchyPath = "";
            exportAnims = false;
            vertextype = Geometry.Enums.GXDataType.Float32;
            fraction = 0;
            material_order_strict = false;
            export_skeleton_root = true;
            skeleton_root_marker = "skeleton_root";
            skeleton_root_name = null;
            skeleton_autodetect = false;
            include_normals = true;
            material_folder = "";
            texture_path = "";
            file_name_as_mat_name = false;
            output_material_folder = "";
            texfraction = 8;
            transform_mode = Scenegraph.Enums.TransformMode.Xsi;
            add_envtex_attribute = false;
            flip_faces = false;
            int positional_arguments = 0;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-m":
                    case "--mat":
                        if (i + 1 >= args.Length)
                            throw new Exception("The parameters were malformed.");

                        materials_path = args[i + 1];
                        i++;
                        break;
                    case "--matfolder":
                        if (i + 1 >= args.Length)
                            throw new Exception("The parameters were malformed.");

                        material_folder = args[i + 1];
                        i++;
                        break;
                    case "--fname_is_matname":
                        file_name_as_mat_name = true;
                        break;
                    case "--texfolder":
                        if (i + 1 >= args.Length)
                            throw new Exception("The parameters were malformed.");

                        texture_path = args[i + 1];
                        i++;
                        break;
                    case "--outmat":
                        if (i + 1 >= args.Length)
                            throw new Exception("The parameters were malformed.");

                        output_materials_path = args[i + 1];
                        i++;
                        break;
                    case "--outmatfolder":
                        if (i + 1 >= args.Length)
                            throw new Exception("The parameters were malformed.");

                        output_material_folder = args[i + 1];
                        i++;
                        break;
                    case "-x":
                    case "--texheader":
                        if (i + 1 >= args.Length)
                            throw new Exception("The parameters were malformed.");

                        texheaders_path = args[i + 1];
                        i++;
                        break;
                    case "-t":
                    case "--tristrip":
                        if (i + 1 >= args.Length)
                            throw new Exception("The parameters were malformed.");

                        tristrip_mode = args[i + 1].ToLower();
                        i++;
                        break;
                    case "-r":
                    case "--rotate":
                        rotate_model = true;
                        break;
                    case "-b":
                    case "--bdl":
                        output_bdl = true;
                        break;
                    case "--profile":
                        do_profile = true;
                        break;
                    case "--nosort":
                        sort_meshes = false;
                        break;
                    case "--onematpermesh":
                        ensure_one_material_per_mesh = true;
                        break;
                    case "--exportobj":
                        export_obj = true;
                        break;
                    case "--texfloat32":
                        forceFloat = true;
                        break;
                    case "--degeneratetri":
                        degenerateTriangles = true;
                        break;
                    case "--nomipmaps":
                        readMipmaps = false;
                        break;
                    case "--sort_strict":
                        sort_strict = true;
                        break;
                    case "--dumphierarchy":
                        dumpHierarchy = true;
                        break;
                    case "--hierarchy":
                        if (i + 1 >= args.Length)
                            throw new Exception("The parameters were malformed.");

                        hierarchyPath = args[i + 1];
                        i++;
                        break;
                    case "-a":
                    case "--animation":
                        exportAnims = true;
                        break;
                    case "--vtxpos":
                        if (i + 2 >= args.Length)
                            throw new Exception("The parameters were malformed.");
                        vertextype = (Geometry.Enums.GXDataType)Enum.Parse(typeof(Geometry.Enums.GXDataType), args[i+1]);
                        fraction = byte.Parse(args[i+2]);
                        i+=2;
                        break;
                    case "--texfraction":
                        if (i + 1 >= args.Length)
                            throw new Exception("The parameters were malformed.");

                        texfraction = fraction = byte.Parse(args[i + 1]);
                        i++;
                        break;
                    case "--mat_strict":
                        material_order_strict = true;
                        break;
                    case "--without_root":
                        export_skeleton_root = false;
                        break;
                    case "--root_marker":
                        if (i + 1 >= args.Length)
                            throw new Exception("The parameters were malformed.");
                        skeleton_root_marker = args[i+1];
                        i++;
                        break;
                    case "--root_name":
                        if (i + 1 >= args.Length)
                            throw new Exception("The parameters were malformed.");
                        skeleton_root_name = args[i + 1];
                        i++;
                        break;
                    case "--root_autodetect":
                        skeleton_autodetect = true;
                        break;
                    case "--no_normals":
                        include_normals = false;
                        break;
                    case "--transform_mode":
                        if (i + 1 >= args.Length)
                            throw new Exception("The parameters were malformed.");
                        transform_mode = (Scenegraph.Enums.TransformMode)Enum.Parse(typeof(Scenegraph.Enums.TransformMode), args[i + 1]);
                        i++;
                        break;
                    case "--envtex_attribute":
                        add_envtex_attribute = true;
                        break;
                    case "--flip_faces":  // Flip face orientation without flipping the normals
                        flip_faces = true;
                        break;
                    default:
                        if (positional_arguments == 0) {
                            positional_arguments += 1;
                            input_path = args[i];
                            break;
                        }
                        else if (positional_arguments == 1) {
                            positional_arguments += 1;
                            output_path = args[i];
                            break;
                        }
                        else {
                            throw new Exception($"Unknown parameter \"{ args[i] }\"");
                        }
                }
            }

            ValidateArgs();
        }

        /// <summary>
        /// Ensures that all the settings parsed from the user's input are valid.
        /// </summary>
        /// <param name="args">Array of settings parsed from the user's input</param>
        private void ValidateArgs()
        {
            // Input
            if (input_path == "")
                throw new Exception("No input file was specified.");
            if (!File.Exists(input_path))
                throw new Exception($"Input file \"{ input_path }\" does not exist.");

            // Output
            if (output_path == "")
            {
                string input_without_ext = Path.Combine(Path.GetDirectoryName(input_path), Path.GetFileNameWithoutExtension(input_path));

                if (input_path.EndsWith(".bmd") || input_path.EndsWith(".bdl"))
                    output_path = input_without_ext + ".dae";
                else
                    output_path = input_without_ext + ".bmd";
            }

            // Material presets
            if (materials_path != "")
            {
                if (!File.Exists(materials_path))
                    throw new Exception($"Material presets file \"{ materials_path }\" does not exist.");
            }

            // Texture headers
            if (texheaders_path != "")
            {
                if (!File.Exists(texheaders_path))
                    throw new Exception($"Texture headers file \"{ texheaders_path }\" does not exist.");
            }

            // Tristrip options
            if (tristrip_mode != "static" && tristrip_mode != "all" && tristrip_mode != "none")
                throw new Exception($"Unknown tristrip option \"{ tristrip_mode }\".");
        }
    }
}
