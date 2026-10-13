"use strict";
(() => {
  const canvas=document.getElementById("constellation-background");
  if(!canvas)return;
  const context=canvas.getContext("2d",{alpha:false});
  if(!context)return;
  const layer=()=>{const canvas=document.createElement("canvas");return{canvas,ctx:canvas.getContext("2d")};};
  const source=layer(),paint=layer(),tint=layer(),reveal=layer(),mask=layer();
  const layers=[source,paint,tint,reveal,mask];
  if(!layers.every(l=>l.ctx))return;
  const image=new Image();
  const reducedMotion=matchMedia("(prefers-reduced-motion: reduce)");
  const pointer={x:0,y:0,inside:false};
  let ready=false,width=0,height=0,scale=1,frame=0,lastMove=0,motion=0;
  let currentColor=document.documentElement.dataset.theme==="light"?"#000000":"#ff354a";
  function black(){context.fillStyle=document.documentElement.dataset.theme==="light"?"#fff":"#000";context.fillRect(0,0,width,height);}
  function clear(l){l.ctx.clearRect(0,0,width,height);}
  function copy(from,to){clear(to);to.ctx.drawImage(from.canvas,0,0,width,height);}
  function makeTint(color){
    clear(tint);tint.ctx.drawImage(source.canvas,0,0,width,height);
    tint.ctx.globalCompositeOperation="source-in";tint.ctx.fillStyle=color;tint.ctx.fillRect(0,0,width,height);
    tint.ctx.globalCompositeOperation="source-over";
  }
  function resize(){
    width=innerWidth;height=innerHeight;
    scale=Math.min(devicePixelRatio||1,1,1600/width,1000/height);
    canvas.width=Math.round(width*scale);canvas.height=Math.round(height*scale);
    context.setTransform(scale,0,0,scale,0,0);
    for(const l of layers){l.canvas.width=canvas.width;l.canvas.height=canvas.height;l.ctx.setTransform(scale,0,0,scale,0,0);}
    black();if(!ready)return;
    const cover=Math.max(width/image.naturalWidth,height/image.naturalHeight);
    const w=image.naturalWidth*cover,h=image.naturalHeight*cover;
    source.ctx.drawImage(image,(width-w)/2,(height-h)/2,w,h);
    makeTint(currentColor);copy(tint,paint);canvas.dataset.color=currentColor;schedule();
  }
  function render(time){
    frame=0;black();if(!ready||!pointer.inside||document.hidden)return;
    const radius=Math.min(240,Math.max(165,width*.16));
    const l=reveal.ctx,m=mask.ctx;clear(reveal);clear(mask);
    l.drawImage(paint.canvas,0,0,width,height);
    const moving=time-lastMove<85;
    // Water responds to pointer movement only.
    if(moving&&motion>.5&&!reducedMotion.matches){
      const x=Math.max(0,pointer.x-radius),y=Math.max(0,pointer.y-radius);
      const w=Math.min(width-x,radius*2),h=Math.min(height-y,radius*2);
      const phase=time*.018;
      for(let row=0;row<h;row+=8){
        const dy=row+y-pointer.y,falloff=Math.max(0,1-Math.abs(dy)/radius);
        const shift=Math.sin(dy*.045-phase)*Math.min(9,motion*.5)*falloff;
        const rowHeight=Math.min(8,h-row);
        if(w>0&&rowHeight>0)l.drawImage(paint.canvas,x*scale,(y+row)*scale,w*scale,rowHeight*scale,x+shift,y+row,w,rowHeight);
      }
    }
    const glow=m.createRadialGradient(pointer.x,pointer.y,0,pointer.x,pointer.y,radius);
    glow.addColorStop(0,"rgba(255,255,255,1)");glow.addColorStop(.42,"rgba(255,255,255,1)");glow.addColorStop(.72,"rgba(255,255,255,.55)");glow.addColorStop(1,"rgba(255,255,255,0)");
    m.fillStyle=glow;m.fillRect(pointer.x-radius,pointer.y-radius,radius*2,radius*2);
    l.globalCompositeOperation="destination-in";l.drawImage(mask.canvas,0,0,width,height);l.globalCompositeOperation="source-over";
    context.drawImage(reveal.canvas,0,0,width,height);
    if(moving)schedule();
  }
  function schedule(){if(!frame&&!document.hidden)frame=requestAnimationFrame(render);}
  function move(event){
    motion=pointer.inside?Math.hypot(event.clientX-pointer.x,event.clientY-pointer.y):0;
    pointer.x=event.clientX;pointer.y=event.clientY;pointer.inside=true;lastMove=performance.now();schedule();
  }
  function leave(){pointer.inside=false;cancelAnimationFrame(frame);frame=0;black();}
  window.addEventListener("mousemove",move,{passive:true});
  window.addEventListener("touchstart",e=>{if(e.touches[0])move(e.touches[0]);},{passive:true});
  window.addEventListener("touchmove",e=>{if(e.touches[0])move(e.touches[0]);},{passive:true});
  window.addEventListener("touchend",leave,{passive:true});
  window.addEventListener("touchcancel",leave,{passive:true});
  window.addEventListener("scroll",()=>{schedule();},{passive:true});
  document.documentElement.addEventListener("mouseleave",leave);
  window.addEventListener("blur",leave);
  window.addEventListener("resize",resize,{passive:true});
  document.addEventListener("visibilitychange",()=>{if(document.hidden)leave();});
  window.addEventListener("portfolio-theme-change",()=>{currentColor=document.documentElement.dataset.theme==="light"?"#000000":"#ff354a";if(ready){makeTint(currentColor);copy(tint,paint);}canvas.dataset.color=currentColor;black();schedule();});
  image.onload=()=>{ready=true;resize();canvas.dataset.ready="true";canvas.dataset.color=currentColor;};
  image.src=canvas.dataset.image;resize();
})();
