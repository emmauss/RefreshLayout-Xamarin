using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Runtime;
using Android.Views;
using Android.Views.Animations;
using Android.Support.V4.View;
using Android.Support.V4.View.Animation;
using Android.Widget;

namespace RefreshLayout
{
    /**
 * Custom progress bar that shows a cycle of colors as widening circles that
 * overdraw each other. When finished, the bar is cleared from the inside out as
 * the main cycle continues. Before running, this can also indicate how close
 * the user is to triggering something (e.g. how far they need to pull down to
 * trigger a refresh).
 */
    public class SwipeProgressBar
    {
        private static readonly bool SUPPORT_CLIP_RECT_DIFFERENCE =
            Build.VERSION.SdkInt >= BuildVersionCodes.JellyBeanMr2;

        // Default progress animation colors are grays.
        private static readonly int COLOR1 = unchecked((int)0xB3000000);
        private static readonly int COLOR2 = unchecked((int)0x80000000);
        private static readonly int COLOR3 = unchecked((int)0x4d000000);
        private static readonly int COLOR4 = unchecked((int)0x1a000000);

        // The duration per color of the animation cycle.
        private static readonly int ANIMATION_DURATION_MS_PER_COLOR = 500;

        // The duration of the animation to clear the bar.
        private static readonly int FINISH_ANIMATION_DURATION_MS = 1000;

        // Interpolator for varying the speed of the animation.
        private static readonly IInterpolator INTERPOLATOR = new FastOutSlowInInterpolator();

        private readonly Paint mPaint = new Paint();
        private readonly RectF mClipRect = new RectF();
        private float mTriggerPercentage;
        private long mStartTime;
        private long mFinishTime;
        private bool mRunning;

        // Colors used when rendering the animation,
        private int[] mColors;
        private int mAnimationDuration;
        private View mParent;

        private Rect mBounds = new Rect();

        public SwipeProgressBar(View parent)
        {
            mParent = parent;
            SetColorScheme(new int[] { COLOR1, COLOR2, COLOR3, COLOR4 });
        }

        /**
         * Set the four colors used in the progress animation. The first color will
         * also be the color of the bar that grows in response to a user swipe
         * gesture.
         *
         * @param colors the colors for scheme
         */
        public void SetColorScheme(int[] colors)
        {
            if (colors == null || colors.Length <= 0)
            {
                throw new Java.Lang.IllegalStateException("colors == null || colors.length <= 0");
            }
            mColors = colors;
            mAnimationDuration = colors.Length * ANIMATION_DURATION_MS_PER_COLOR;
        }

        /**
         * Update the progress the user has made toward triggering the swipe
         * gesture. and use this value to update the percentage of the trigger that
         * is shown.
         */
        public float TriggerPercentage
        {
            set
            {
                mTriggerPercentage = value;
                mStartTime = 0;
                ViewCompat.PostInvalidateOnAnimation(
                        mParent, mBounds.Left, mBounds.Top, mBounds.Right, mBounds.Bottom);
            }
        }

        /**
         * Start showing the progress animation.
         */
       public void Start()
        {
            if (!mRunning)
            {
                mTriggerPercentage = 0;
                mStartTime = AnimationUtils.CurrentAnimationTimeMillis();
                mRunning = true;
                mParent.PostInvalidate();
            }
        }

        /**
         * Stop showing the progress animation.
         */
        public void Stop()
        {
            if (mRunning)
            {
                mTriggerPercentage = 0;
                mFinishTime = AnimationUtils.CurrentAnimationTimeMillis();
                mRunning = false;
                mParent.PostInvalidate();
            }
        }

        /**
         * @return Return whether the progress animation is currently running.
         */
        bool isRunning()
        {
            return mRunning || mFinishTime > 0;
        }

        public void Draw(Canvas canvas)
        {
            // API < 18 do not support clipRect(Region.Op.DIFFERENCE).
            // So draw twice for finish animation
            if (Draw(canvas, true))
            {
                Draw(canvas, false);
            }
        }

        private bool Draw(Canvas canvas, bool first)
        {
            Rect bounds = mBounds;
            int width = bounds.Width();
            int cx = bounds.CenterX();
            int cy = bounds.CenterY();
            int colors = mColors.Length;
            bool drawTriggerWhileFinishing = false;
            bool drawAgain = false;
            int restoreCount = canvas.Save();
            canvas.ClipRect(bounds);

            if (mRunning || (mFinishTime > 0))
            {
                long now = AnimationUtils.CurrentAnimationTimeMillis();
                long elapsed = (now - mStartTime) % mAnimationDuration;
                long iterations = (now - mStartTime) / ANIMATION_DURATION_MS_PER_COLOR;
                float rawProgress = (elapsed / (mAnimationDuration / (float)colors));

                // If we're not running anymore, that means we're running through
                // the finish animation.
                if (!mRunning)
                {
                    // If the finish animation is done, don't draw anything, and
                    // don't repost.
                    if ((now - mFinishTime) >= FINISH_ANIMATION_DURATION_MS)
                    {
                        mFinishTime = 0;
                        return false;
                    }

                    // Otherwise, use a 0 opacity alpha layer to clear the animation
                    // from the inside out. This layer will prevent the circles from
                    // drawing within its bounds.
                    long finishElapsed = (now - mFinishTime) % FINISH_ANIMATION_DURATION_MS;
                    float finishProgress = (finishElapsed / (FINISH_ANIMATION_DURATION_MS / 100f));
                    float pct = (finishProgress / 100f);
                    // Radius of the circle is half of the screen.
                    float clearRadius = width / 2 * INTERPOLATOR.GetInterpolation(pct);
                    if (SUPPORT_CLIP_RECT_DIFFERENCE)
                    {
                        mClipRect.Set(cx - clearRadius, bounds.Top, cx + clearRadius, bounds.Bottom);
                        canvas.ClipRect(mClipRect, Region.Op.Difference);
                    }
                    else
                    {
                        if (first)
                        {
                            // First time left
                            drawAgain = true;
                            mClipRect.Set(bounds.Left, bounds.Top, cx - clearRadius, bounds.Bottom);
                        }
                        else
                        {
                            // Second time right
                            mClipRect.Set(cx + clearRadius, bounds.Top, bounds.Right, bounds.Bottom);
                        }
                        canvas.ClipRect(mClipRect);
                    }
                    // Only draw the trigger if there is a space in the center of
                    // this refreshing view that needs to be filled in by the
                    // trigger. If the progress view is just still animating, let it
                    // continue animating.
                    drawTriggerWhileFinishing = true;
                }

                // First fill in with the last color that would have finished drawing.
                if (iterations == 0)
                {
                    canvas.DrawColor(new Color(mColors[0]));
                }
                else
                {
                    int index = colors - 1;
                    float left = 0.0f;
                    float right = 1.0f;
                    for (int i = 0; i < colors; ++i)
                    {
                        if ((rawProgress >= left && rawProgress < right) || i == colors - 1)
                        {
                            canvas.DrawColor(new Color(mColors[index]));
                            break;
                        }
                        index = (index + 1) % colors;
                        left += 1.0f;
                        right += 1.0f;
                    }
                }

                // Then draw up to 4 overlapping concentric circles of varying radii, based on how far
                // along we are in the cycle.
                // progress 0-50 draw mColor2
                // progress 25-75 draw mColor3
                // progress 50-100 draw mColor4
                // progress 75 (wrap to 25) draw mColor1
                if (colors > 1)
                {
                    if ((rawProgress >= 0.0f && rawProgress <= 1.0f))
                    {
                        float pct = (rawProgress + 1.0f) / 2;
                        drawCircle(canvas, cx, cy, mColors[0], pct);
                    }
                    float left = 0.0f;
                    float right = 2.0f;
                    for (int i = 1; i < colors; ++i)
                    {
                        if (rawProgress >= left && rawProgress <= right)
                        {
                            float pct = (rawProgress - i + 1.0f) / 2;
                            drawCircle(canvas, cx, cy, mColors[i], pct);
                        }
                        left += 1.0f;
                        right += 1.0f;
                    }
                    if ((rawProgress >= colors - 1.0f && rawProgress <= colors))
                    {
                        float pct = (rawProgress - colors + 1.0f) / 2;
                        drawCircle(canvas, cx, cy, mColors[0], pct);
                    }
                }
                if (mTriggerPercentage > 0 && drawTriggerWhileFinishing)
                {
                    // There is some portion of trigger to draw. Restore the canvas,
                    // then draw the trigger. Otherwise, the trigger does not appear
                    // until after the bar has finished animating and appears to
                    // just jump in at a larger width than expected.
                    canvas.RestoreToCount(restoreCount);
                    restoreCount = canvas.Save();
                    canvas.ClipRect(bounds);
                    drawTrigger(canvas, cx, cy);
                }
                // Keep running until we finish out the last cycle.
                ViewCompat.PostInvalidateOnAnimation(
                        mParent, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
            }
            else
            {
                // Otherwise if we're in the middle of a trigger, draw that.
                if (mTriggerPercentage > 0 && mTriggerPercentage <= 1.0)
                {
                    drawTrigger(canvas, cx, cy);
                }
            }
            canvas.RestoreToCount(restoreCount);
            return drawAgain;
        }

        private void drawTrigger(Canvas canvas, int cx, int cy)
        {
            mPaint.Color = new Color(mColors[0]);
            canvas.DrawCircle(cx, cy, cx * mTriggerPercentage, mPaint);
        }

        /**
         * Draws a circle centered in the view.
         *
         * @param canvas the canvas to draw on
         * @param cx the center x coordinate
         * @param cy the center y coordinate
         * @param color the color to draw
         * @param pct the percentage of the view that the circle should cover
         */
        private void drawCircle(Canvas canvas, float cx, float cy, int color, float pct)
        {
            mPaint.Color = new Color(color);
            canvas.Save();
            canvas.Translate(cx, cy);
            float radiusScale = INTERPOLATOR.GetInterpolation(pct);
            canvas.Scale(radiusScale, radiusScale);
            canvas.DrawCircle(0, 0, cx, mPaint);
            canvas.Restore();
        }

        /**
         * Set the drawing bounds of this SwipeProgressBar.
         */
        public void SetBounds(int left, int top, int right, int bottom)
        {
            mBounds.Left = left;
            mBounds.Top = top;
            mBounds.Right = right;
            mBounds.Bottom = bottom;
        }

    }
}