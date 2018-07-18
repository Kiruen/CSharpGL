﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpGL;
using System.Drawing;
using System.Runtime.InteropServices;

namespace fuluDd00_VolumeMapping
{
    partial class PeelingNode : SceneNodeBase, IRenderable, IVolumeData
    {
        private PeelingResource resources;
        private Query query;
        private bool bUseOQ = true;
        private const int NUM_PASSES = 16;
        private DepthTestSwitch depthTest = new DepthTestSwitch(enableCapacity: false);
        private ShaderProgram engraveComp;
        private VertexBuffer outBuffer;

        public PeelingNode(params SceneNodeBase[] children)
        {
            this.query = new Query();
            this.Children.AddRange(children);
            {
                var cs = new CSharpGL.ComputeShader(Shaders.engraveComp);
                var provider = new ShaderArray(cs);
                this.engraveComp = provider.GetShaderProgram();
            }
            //{
            //    var storage = new TexStorage3D(TexStorage3D.Target.Texture3D, GL.GL_RED, this.Width, this.Height, this.Depth);
            //    var texture = new Texture(storage);
            //    texture.Initialize();
            //    this.texVolumeData = texture;
            //}
            {
                var data = new uint[Width * Height * Depth];
                this.outBuffer = data.GenVertexBuffer(VBOConfig.UInt, BufferUsage.DynamicCopy);
            }
            {
                InitializePeelingResource(vWidth, vHeight);
            }
        }

        /// <summary>
        /// max step needed to render everything.
        /// </summary>
        private const int maxStep = 1 + ((NUM_PASSES - 1) * 2 - 1) * 2;

        private int renderStep = 1 + ((NUM_PASSES - 1) * 2 - 1) * 2;
        /// <summary>
        /// How many steps will be performed?
        /// </summary>
        public int RenderStep
        {
            get { return renderStep; }
            set { renderStep = value; }
        }

        #region IRenderable 成员

        public ThreeFlags EnableRendering { get { return ThreeFlags.BeforeChildren; } set { } }

        private bool firstRun = true;

        public bool FirstRun
        {
            get { return firstRun; }
            set { firstRun = value; }
        }

        const int vWidth = 256;
        const int vHeight = 256;
        const int vDepth = 256;
        private byte[] volumeData;// = new byte[vWidth * vHeight * vDepth];

        public byte[] VolumeData
        {
            get { return volumeData; }
        }

        private Texture texVolumeData;

        public Texture TexVolumeData
        {
            get { return this.texVolumeData; }
        }

        public int Width { get { return vWidth; } }
        public int Height { get { return vHeight; } }
        public int Depth { get { return vDepth; } }

        public unsafe void RenderBeforeChildren(RenderEventArgs arg)
        {
            if (!this.firstRun) { return; }


            //var viewport = new int[4]; GL.Instance.GetIntegerv((uint)GetTarget.Viewport, viewport);

            {
                var position = new vec3(0, 0, 0);
                var center = new vec3(0, 0, -1);
                //var center = new vec3(-3, -4, -5);
                var up = new vec3(0, 1, 0);
                var camera = new Camera(position, center, up, CameraType.Ortho, vWidth, vHeight);
                {
                    vec3 size = this.ModelSize;
                    IOrthoViewCamera c = camera;
                    c.Left = -size.x / 2; c.Right = size.x / 2;
                    c.Bottom = -size.y / 2; c.Top = size.y / 2;
                    c.Near = -size.z / 2; c.Far = size.z / 2;
                    //c.Left = -size.x; c.Right = size.x;
                    //c.Bottom = -size.y; c.Top = size.y;
                    //c.Near = -size.z; c.Far = size.z;
                }
                arg = new RenderEventArgs(arg.Param, camera);
            }

            int currentStep = 0, totalStep = this.RenderStep;
            this.resources.blenderFBO.Bind();
            GL.Instance.Clear(GL.GL_COLOR_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT);
            this.resources.blenderFBO.Unbind();
            Texture targetTexture = this.resources.blenderColorTexture;

            var bitmapList = new List<Bitmap>();
            // remember clear color.
            var clearColor = new float[4];
            GL.Instance.GetFloatv((uint)GetTarget.ColorClearValue, clearColor);

            // init.
            if (currentStep <= totalStep)
            {
                currentStep++;
                this.resources.blenderFBO.Bind();
                GL.Instance.ClearColor(0, 0, 0, 0);
                GL.Instance.Clear(GL.GL_COLOR_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT);
                this.DrawScene(arg, CubeNode.RenderMode.Init, null);
                this.resources.blenderFBO.Unbind();
                targetTexture = this.resources.blenderColorTexture;

                if (firstRun)
                {
                    var bitmap = targetTexture.GetImage(vWidth, vHeight);
                    bitmap.Save("0.init.png");
                    //var image = (Bitmap)bitmap.GetThumbnailImage(vWidth, vHeight, null, IntPtr.Zero);
                    bitmapList.Add(bitmap);
                    //bitmap.Dispose();
                    //ComputeShaderEngrave(targetTexture, this.texVolumeData, vWidth, vHeight);
                    var image = new Bitmap("0.init.png");
                    var texture = this.GetTexture(image);
                    image.Dispose();
                    ComputeShaderEngrave(texture, this.outBuffer, vWidth, vHeight);
                    texture.Dispose();
                }
            }

            int numLayers = (NUM_PASSES - 1) * 2;
            int finalId = 2;
            // for each pass
            for (int layer = 1; (bUseOQ || layer < numLayers) && (currentStep <= totalStep); layer++)
            {
                finalId = layer * 2 + 1;
                int currId = layer % 2;
                int prevId = 1 - currId;
                bool sampled = true;
                // peel.
                if (currentStep <= totalStep)
                {
                    currentStep++;
                    this.resources.FBOs[currId].Bind();
                    GL.Instance.ClearColor(0, 0, 0, 0);
                    GL.Instance.Clear(GL.GL_COLOR_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT);
                    if (bUseOQ) { this.query.BeginQuery(QueryTarget.SamplesPassed); }
                    this.DrawScene(arg, CubeNode.RenderMode.Peel, this.resources.depthTextures[prevId]);
                    if (bUseOQ) { this.query.EndQuery(QueryTarget.SamplesPassed); }
                    this.resources.FBOs[currId].Unbind();
                    targetTexture = this.resources.colorTextures[currId];

                    if (bUseOQ)
                    {
                        int sampleCount = this.query.SampleCount();
                        sampled = (sampleCount > 0);
                    }

                    if (firstRun && sampled)
                    {
                        var bitmap = targetTexture.GetImage(vWidth, vHeight);
                        bitmap.Save(string.Format("{0}.peel.png", layer * 2 - 1));
                        //var image = (Bitmap)bitmap.GetThumbnailImage(vWidth, vHeight, null, IntPtr.Zero);
                        bitmapList.Add(bitmap);
                        //bitmap.Dispose();
                        //ComputeShaderEngrave(targetTexture, this.texVolumeData, vWidth, vHeight);
                        var image = new Bitmap(string.Format("{0}.peel.png", layer * 2 - 1));
                        var texture = this.GetTexture(image);
                        image.Dispose();
                        ComputeShaderEngrave(texture, this.outBuffer, vWidth, vHeight);
                        texture.Dispose();
                    }
                }
            }

            Color background = Color.SkyBlue;
            int count = 0;
            byte minA = byte.MaxValue;
            byte maxA = byte.MinValue;
            var volumeData = new byte[vWidth * vHeight * vDepth]; ;
            foreach (var bitmap in bitmapList)
            {
                for (int w = 0; w < vWidth; w++)
                {
                    for (int h = 0; h < vHeight; h++)
                    {
                        Color color = bitmap.GetPixel(w, h);
                        if (color.A < minA) { minA = color.A; }
                        if (maxA < color.A) { maxA = color.A; }
                        int d = (int)((double)vDepth * (double)color.A / 256.0);
                        int index = w * vHeight * vDepth + h * vDepth + d;
                        if (color.A != 0 &&
                            (color.R != background.R || color.G != background.G || color.B != background.B))
                        {
                            volumeData[index] += (byte)(color.R * 0.299 + color.G * 0.587 + color.B * 0.114);
                            count++;
                        }
                    }
                }
                bitmap.Dispose();
            }

            this.volumeData = volumeData;
            // texVolumeData => *.dat
            //{
            //    var texture = this.texVolumeData;
            //    texture.Bind();

            //    var array = new byte[Width * Height * Depth];
            //    GCHandle pinned = GCHandle.Alloc(array, GCHandleType.Pinned);
            //    IntPtr header = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0);
            //    GL.Instance.GetTexImage((uint)texture.Target, 0, GL.GL_RED, GL.GL_UNSIGNED_BYTE, header);
            //    pinned.Free();

            //    texture.Unbind();

            //    for (int i = 0; i < array.Length; i++)
            //    {
            //        if (array[i] != 0) { Console.WriteLine("adsf"); }
            //    }

            //    Console.WriteLine("asdfF");
            //}
            {
                VertexBuffer buffer = this.outBuffer;
                var array = (uint*)buffer.MapBuffer(MapBufferAccess.ReadWrite);
                for (int i = 0; i < this.volumeData.Length; i++)
                {
                    //this.volumeData[i] = (byte)array[i];
                    //if (this.volumeData[i] != array[i]) { Console.WriteLine("adsf"); }
                    //if (array[i] != 0) { Console.WriteLine("asdf"); }
                }
                buffer.UnmapBuffer();
            }

            this.firstRun = false;
        }
        internal static GLDelegates.void_uint_uint_uint glBindBufferBase;

        private void ComputeShaderEngrave(Texture texInput, VertexBuffer outBuffer, int width, int height)
        {
            if (glBindBufferBase == null) { glBindBufferBase = GL.Instance.GetDelegateFor("glBindBufferBase", GLDelegates.typeof_void_uint_uint_uint) as GLDelegates.void_uint_uint_uint; }

            // engrave volume data with compute shader.
            uint inputUnit = 0, outputUnit = 1;
            ShaderProgram computeProgram = this.engraveComp;
            // Activate the compute program and bind the output texture image
            computeProgram.Bind();
            //glBindImageTexture(outputUnit, outBuffer.BufferId, 0, false, 0, GL.GL_READ_WRITE, GL.GL_RED);
            glBindBufferBase(GL.GL_SHADER_STORAGE_BUFFER, outputUnit, outBuffer.BufferId);
            glBindImageTexture(inputUnit, texInput.Id, 0, false, 0, GL.GL_READ_WRITE, GL.GL_RGBA32F);
            // Dispatch
            glDispatchCompute(1, (uint)height, 1);
            //glDispatchCompute((uint)width, (uint)height, 1);
            glMemoryBarrier(GL.GL_SHADER_IMAGE_ACCESS_BARRIER_BIT);
            glBindImageTexture(inputUnit, 0, 0, false, 0, GL.GL_READ_WRITE, GL.GL_RGBA32F);
            //glBindImageTexture(outputUnit, 0, 0, false, 0, GL.GL_READ_WRITE, GL.GL_RED);
            computeProgram.Unbind();
        }
        //private void ComputeShaderEngrave(Texture texInput, Texture texOutput, int width, int height)
        //{
        //    // engrave volume data with compute shader.
        //    uint inputUnit = 0, outputUnit = 1;
        //    ShaderProgram computeProgram = this.engraveComp;
        //    // Activate the compute program and bind the output texture image
        //    computeProgram.Bind();
        //    glBindImageTexture(inputUnit, texInput.Id, 0, false, 0, GL.GL_READ_WRITE, GL.GL_RGBA32F);
        //    glBindImageTexture(outputUnit, texOutput.Id, 0, true, 0, GL.GL_READ_WRITE, GL.GL_RED);
        //    // Dispatch
        //    glDispatchCompute(1, (uint)height, 1);
        //    //glDispatchCompute((uint)width, (uint)height, 1);
        //    glMemoryBarrier(GL.GL_SHADER_IMAGE_ACCESS_BARRIER_BIT);
        //    glBindImageTexture(inputUnit, 0, 0, false, 0, GL.GL_READ_WRITE, GL.GL_RGBA32F);
        //    glBindImageTexture(outputUnit, 0, 0, true, 0, GL.GL_READ_WRITE, GL.GL_RED);
        //    computeProgram.Unbind();
        //}

        private Texture GetTexture(Bitmap bitmap)
        {
            //bitmap.RotateFlip(RotateFlipType.Rotate180FlipX);

            var texture = new Texture(new TexImageBitmap(bitmap, GL.GL_RGBA32F));
            texture.BuiltInSampler.Add(new TexParameteri(TexParameter.PropertyName.TextureWrapS, (int)GL.GL_CLAMP_TO_EDGE));
            texture.BuiltInSampler.Add(new TexParameteri(TexParameter.PropertyName.TextureWrapT, (int)GL.GL_CLAMP_TO_EDGE));
            texture.BuiltInSampler.Add(new TexParameteri(TexParameter.PropertyName.TextureWrapR, (int)GL.GL_CLAMP_TO_EDGE));
            texture.BuiltInSampler.Add(new TexParameteri(TexParameter.PropertyName.TextureMinFilter, (int)GL.GL_LINEAR));
            texture.BuiltInSampler.Add(new TexParameteri(TexParameter.PropertyName.TextureMagFilter, (int)GL.GL_LINEAR));

            texture.Initialize();

            return texture;
        }

        private void InitializePeelingResource(int width, int height)
        {
            if (this.resources != null) { this.resources.Dispose(); }
            this.resources = new PeelingResource(width, height);
        }

        private void DrawScene(RenderEventArgs arg, CubeNode.RenderMode renderMode, Texture texture)
        {
            foreach (var item in this.Children)
            {
                var node = item as CubeNode;
                node.Mode = renderMode;
                if (texture != null)
                {
                    node.DepthTexture = texture;
                }
                node.RenderBeforeChildren(arg);
            }
        }

        public void RenderAfterChildren(RenderEventArgs arg)
        {
        }

        #endregion
    }
}
