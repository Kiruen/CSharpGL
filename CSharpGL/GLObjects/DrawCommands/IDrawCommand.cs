﻿using System.ComponentModel;
using System.Drawing.Design;

namespace CSharpGL
{
    /// <summary>
    /// Execute some drawing command(glDrawArrays etc..).
    /// </summary>
    [Editor(typeof(DrawElementsCmdEditor), typeof(UITypeEditor))]
    public interface IDrawCommand
    {
        /// <summary>
        /// 用哪种方式渲染各个顶点？（GL.GL_TRIANGLES etc.）
        /// </summary>
        DrawMode Mode { get; }

        /// <summary>
        /// 用哪种方式渲染各个顶点？（GL.GL_TRIANGLES etc.）
        /// </summary>
        DrawMode CurrentMode { get; set; }

        /// <summary>
        /// 执行渲染操作。
        /// <para>Render.</para>
        /// </summary>
        /// <param name="indexAccessMode">index buffer is accessable randomly or only by frame.</param>
        void Draw(IndexAccessMode indexAccessMode);

    }

    /// <summary>
    /// 
    /// </summary>
    public enum IndexAccessMode
    {
        /// <summary>
        /// 
        /// </summary>
        Random,

        /// <summary>
        /// 
        /// </summary>
        ByFrame,

    }
}