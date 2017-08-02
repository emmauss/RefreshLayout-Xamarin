using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Graphics.Drawables.Shapes;
using Android.Support.V4.Content;
using Android.Views.Animations;
using Android.Support.V4.View;
using Android.Support.V4;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace RefreshLayout
{
    public class CircleImageView : ImageView
    {
        private static readonly int KEY_SHADOW_COLOR = 0x1E000000;
        private static readonly int FILL_SHADOW_COLOR = 0x3D000000;
        // PX
        private static readonly float X_OFFSET = 0f;
        private static readonly float Y_OFFSET = 1.75f;
        private static readonly float SHADOW_RADIUS = 3.5f;
        private static readonly int SHADOW_ELEVATION = 4;

        private Animation.IAnimationListener mListener;
        int mShadowRadius;

        public CircleImageView(Context context, int color) :base(context)
        {
             float density = Context.Resources.DisplayMetrics.Density;
             int shadowYOffset = (int)(density * Y_OFFSET);
             int shadowXOffset = (int)(density * X_OFFSET);

            mShadowRadius = (int)(density * SHADOW_RADIUS);

            ShapeDrawable circle;
            if (ElevationSupported)
            {
                circle = new ShapeDrawable(new OvalShape());
                ViewCompat.SetElevation(this, SHADOW_ELEVATION * density);
            }
            else
            {
                OvalShape oval = new OvalShadow(mShadowRadius,this);
                circle = new ShapeDrawable(oval);
                ViewCompat.SetLayerType(this, ViewCompat.LayerTypeSoftware, circle.Paint);
                circle.Paint.SetShadowLayer(mShadowRadius, shadowXOffset, shadowYOffset,
                        new Color(KEY_SHADOW_COLOR));
                 int padding = mShadowRadius;
                // set padding so the inner image sits correctly within the shadow.
                SetPadding(padding, padding, padding, padding);
            }
            circle.Paint.Color =new Color(color);
            
            
            Background = circle;
        }

        private bool ElevationSupported
        {
            get
            {
                return Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop;
            }
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
            if (!ElevationSupported)
            {
                SetMeasuredDimension(MeasuredWidth + mShadowRadius * 2, MeasuredHeight
                        + mShadowRadius * 2);
            }
        }

        public void SetAnimationListener(Animation.IAnimationListener listener)
        {
            mListener = listener;
        }

        protected override void OnAnimationStart()
        {
            base.OnAnimationStart();
            if (mListener != null)
            {
                mListener.OnAnimationEnd(Animation);
            }
        }

        public void SetBackgroundColorRes(int colorRes)
        {
            SetBackgroundColor(new Color(ContextCompat.GetColor(Context, colorRes)));
        }

        public override void SetBackgroundColor(Color color)
        {
            if (Background is ShapeDrawable) {
                ((ShapeDrawable)Background).Paint.Color = color;
            }

        }

        public class OvalShadow : OvalShape
        {
            private RadialGradient mRadialGradient;
            private Paint mShadowPaint;
            private int mShadowRadius;
            private CircleImageView circle;
             public OvalShadow(int shadowRadius, CircleImageView circle) : base()
            {
                this.circle = circle;
                mShadowPaint = new Paint();
                mShadowRadius = shadowRadius;
                UpdateRadialGradient((int)Rect().Width());
            }

            private void UpdateRadialGradient(int diameter)
            {
                mRadialGradient = new RadialGradient(diameter / 2, diameter / 2,
                        mShadowRadius, new int[] { FILL_SHADOW_COLOR, Color.Transparent },
                        null, Shader.TileMode.Clamp);
                mShadowPaint.SetShader(mRadialGradient);
            }

            public override void Draw(Canvas canvas, Paint paint)
            {
                
                int viewWidth = circle.Width;
                int viewHeight = circle.Height;
                canvas.DrawCircle(viewWidth / 2, viewHeight / 2, viewWidth / 2, mShadowPaint);
                canvas.DrawCircle(viewWidth / 2, viewHeight / 2, viewWidth / 2 - mShadowRadius, paint);
            }
        }
       
    }
}