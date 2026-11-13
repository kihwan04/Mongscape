using Muhanok.Domain;
using Muhanok.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Muhanok.Tests
{
    public sealed class RouteViewportTests
    {
        [TestCase(1920,1080)] [TestCase(1702,726)] [TestCase(1024,768)] [TestCase(720,1280)]
        public void ViewportFitsWithoutStretchOrCrop(int width,int height)
        {
            var safe=new Rect(0,0,width,height); var r=ViewportLayout.Fit(width,height,safe);
            Assert.AreEqual(16f/9f,r.width/r.height,.0001f);
            Assert.GreaterOrEqual(r.xMin,0); Assert.LessOrEqual(r.xMax,width);
            Assert.GreaterOrEqual(r.yMin,0); Assert.LessOrEqual(r.yMax,height);
            Assert.AreEqual(width*.5f,r.center.x,.001f); Assert.AreEqual(height*.5f,r.center.y,.001f);
        }
        [Test] public void SafeAreaIncludesAsymmetricInsets()
        {
            var safe=new Rect(48,24,1780,992); var r=ViewportLayout.Fit(1920,1080,safe);
            Assert.GreaterOrEqual(r.xMin,safe.xMin); Assert.LessOrEqual(r.xMax,safe.xMax);
            Assert.GreaterOrEqual(r.yMin,safe.yMin); Assert.LessOrEqual(r.yMax,safe.yMax);
        }
        [Test] public void AllRouteBoundariesCloseInHeightPositionAndTangent()
        {
            for(int sequence=0;sequence<120;sequence++)
            {
                int type=RouteSurface.TypeAt(sequence), next=RouteSurface.TypeAt(sequence+1);
                Assert.AreEqual(RouteSurface.BaseHeight(sequence+1),RouteSurface.BaseHeight(sequence)+RouteSurface.Rise(type),.00001f);
                RouteSurface.Sample((sequence+1)*24-.001f,out float x0,out float y0);
                RouteSurface.Sample((sequence+1)*24+.001f,out float x1,out float y1);
                Assert.AreEqual(x0,x1,.00001f); Assert.AreEqual(y0,y1,.00001f);
                Assert.AreEqual(0,RouteSurface.LocalX(type,24)-RouteSurface.LocalX(type,23.999f),.00001f);
            }
            Assert.AreEqual(RouteSurface.StairRise,RouteSurface.LocalHeight(7,24)); Assert.AreEqual(-3,RouteSurface.LocalHeight(11,24));
            Assert.Greater(RouteSurface.LocalX(1,12),1f);
        }
    }
}
