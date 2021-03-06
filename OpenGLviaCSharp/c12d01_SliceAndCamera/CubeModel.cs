﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpGL;

namespace c12d01_SliceAndCamera
{
    /// <summary>
    ///        Y
    ///        |
    ///        5___________1
    ///       /|          /|
    ///      / |         / |
    ///     4--+--------0  |
    ///     |  7_ _ _ _ |_ 3____ X
    ///     |  /        |  /
    ///     | /         | /
    ///     |/__________|/
    ///     6           2
    ///    /
    ///   Z
    /// </summary>
    class CubeModel : IBufferSource
    {
        private const float halfLength = 0.5f;
        private static readonly vec3[] positions = new vec3[]
        {
            new vec3(+halfLength, +halfLength, +halfLength), // 0
            new vec3(+halfLength, +halfLength, -halfLength), // 1
            new vec3(+halfLength, -halfLength, +halfLength), // 2
            new vec3(+halfLength, -halfLength, -halfLength), // 3 
            new vec3(-halfLength, +halfLength, +halfLength), // 4 
            new vec3(-halfLength, +halfLength, -halfLength), // 5
            new vec3(-halfLength, -halfLength, +halfLength), // 6
            new vec3(-halfLength, -halfLength, -halfLength), // 7
        };

        private static readonly uint[] indexes = new uint[]
        {
            0, 2, 3, 1,
            0, 1, 5, 4,
            0, 4, 6, 2,
            7, 6, 4, 5,
            7, 5, 1, 3,
            7, 3, 2, 6,
        };

        public const string strPosition = "position";
        private VertexBuffer positionBuffer;

        private IDrawCommand drawCommand;


        #region IBufferSource 成员

        public IEnumerable<VertexBuffer> GetVertexAttribute(string bufferName)
        {
            if (strPosition == bufferName) // requiring position buffer.
            {
                if (this.positionBuffer == null)
                {
                    this.positionBuffer = positions.GenVertexBuffer(VBOConfig.Vec3, BufferUsage.StaticDraw);
                }

                yield return this.positionBuffer;
            }
            else
            {
                throw new ArgumentException("bufferName");
            }
        }

        public IEnumerable<IDrawCommand> GetDrawCommand()
        {
            if (this.drawCommand == null)
            {
                IndexBuffer indexBuffer = indexes.GenIndexBuffer(BufferUsage.StaticDraw);
                this.drawCommand = new DrawElementsCmd(indexBuffer, DrawMode.Quads);
            }

            yield return this.drawCommand;
        }

        #endregion
    }
}
