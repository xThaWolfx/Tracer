﻿using System;
using System.ComponentModel;

namespace Tracer.Classes.Util
{
    [Serializable]
    [TypeConverter( typeof ( ExpandableObjectConverter ) )]
    public class Color
    {
        public static Color White
        {
            get { return new Color( 1f, 1f, 1f ); }
        }

        public static Color Black
        {
            get { return new Color( 0f, 0f, 0f ); }
        }

        [DisplayName( "Red" )]
        [Description( "The red component of the color." )]
        public float R { set; get; }

        [DisplayName( "Green" )]
        [Description( "The green component of the color." )]
        public float G { set; get; }

        [DisplayName( "Blue" )]
        [Description( "The blue component of the color." )]
        public float B { set; get; }

        [Browsable( false )]
        public byte bR
        {
            get { return ( byte ) ( R * 255f ); }
        }

        [Browsable( false )]
        public byte bG
        {
            get { return ( byte ) ( G * 255f ); }
        }

        [Browsable( false )]
        public byte bB
        {
            get { return ( byte ) ( B * 255f ); }
        }

        [Browsable( false )]
        public System.Drawing.Color DrawingColor
        {
            get
            {
                Color C = Clamped;
                return System.Drawing.Color.FromArgb( C.bR, C.bG, C.bB );
            }
        }

        [Browsable( false )]
        public Color Clamped
        {
            get { return new Color( Math.Min( 1f, R ), Math.Min( 1f, G ), Math.Min( 1f, B ) ); }
        }

        public Color( float R, float G, float B )
        {
            this.R = R;
            this.G = G;
            this.B = B;
        }

        public Color( )
        {
            this.R = 0.0f;
            this.G = 0.0f;
            this.B = 0.0f;
        }

        public override string ToString( )
        {
            return string.Format( "( {0}, {1}, {2} )", R, G, B );
        }

        #region Operators

        public static Color operator *( Color C1, float M )
        {
            return new Color( C1.R * M, C1.G * M, C1.B * M );
        }

        public static Color operator /( Color C1, float M )
        {
            return new Color( C1.R / M, C1.G / M, C1.B / M );
        }

        public static Color operator +( Color C1, Color C2 )
        {
            return new Color( C1.R + C2.R, C1.G + C2.G, C1.B + C2.B );
        }

        public static Color operator *( Color C1, Color C2 )
        {
            return new Color( C1.R * C2.R, C1.G * C2.G, C1.B * C2.B );
        }

        public static bool operator ==( Color C1, Color C2 )
        {
            return C1.bR == C2.bR && C1.bG == C2.bG && C1.bB == C2.bB;
        }

        public static bool operator !=( Color C1, Color C2 )
        {
            return !( C1 == C2 );
        }

        #endregion
    }
}