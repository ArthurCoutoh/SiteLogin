"use strict";
(()=>{
 const canvas=document.getElementById('login-hands'),box=document.getElementById('login-art'),ctx=canvas.getContext('2d');if(!ctx)return;
 const layer=document.createElement('canvas'),ink=layer.getContext('2d'),reveal=document.createElement('canvas'),light=reveal.getContext('2d'),image=new Image();let w=0,h=0,ready=false,frame=0,last=0,speed=0;
 const point={x:0,y:0,inside:false},reduced=matchMedia('(prefers-reduced-motion: reduce)');
 function draw(time){frame=0;ctx.clearRect(0,0,w,h);if(!ready||!point.inside)return;
  light.clearRect(0,0,w,h);const gradient=light.createRadialGradient(point.x,point.y,0,point.x,point.y,180);gradient.addColorStop(0,'#fff');gradient.addColorStop(1,'transparent');
  const moving=time-last<85&&!reduced.matches&&speed>.5;
  if(moving){for(let row=0;row<h;row+=8){const shift=Math.sin((row-point.y)*.05-time*.018)*Math.min(8,speed*.4);light.drawImage(layer,0,row,w,Math.min(8,h-row),shift,row,w,Math.min(8,h-row));}}else light.drawImage(layer,0,0,w,h);
  light.globalCompositeOperation='destination-in';light.fillStyle=gradient;light.fillRect(0,0,w,h);light.globalCompositeOperation='source-over';ctx.drawImage(reveal,0,0,w,h);if(moving)schedule();
 }
 function schedule(){if(!frame)frame=requestAnimationFrame(draw);}
 function resize(){const rect=box.getBoundingClientRect();w=Math.max(1,Math.round(rect.width));h=Math.max(1,Math.round(rect.height));canvas.width=layer.width=reveal.width=w;canvas.height=layer.height=reveal.height=h;if(ready){const scale=Math.min(w/image.naturalWidth,h/image.naturalHeight)*1.22;const iw=image.naturalWidth*scale,ih=image.naturalHeight*scale;ink.clearRect(0,0,w,h);ink.drawImage(image,(w-iw)/2,(h-ih)/2,iw,ih);ink.globalCompositeOperation='source-in';ink.fillStyle=document.documentElement.dataset.theme==='light'?'#000':'#ff354a';ink.fillRect(0,0,w,h);ink.globalCompositeOperation='source-over';}schedule();}
 function move(e){const rect=box.getBoundingClientRect(),x=e.clientX-rect.left,y=e.clientY-rect.top;speed=point.inside?Math.hypot(x-point.x,y-point.y):0;Object.assign(point,{x,y,inside:true});last=performance.now();schedule();}box.addEventListener('pointermove',move,{passive:true});box.addEventListener('pointerdown',move,{passive:true});
 function leave(){point.inside=false;schedule();}box.addEventListener('pointerleave',leave);box.addEventListener('pointercancel',leave);box.addEventListener('pointerup',e=>{if(e.pointerType==='touch')leave();});window.addEventListener('blur',leave);window.addEventListener('resize',resize);window.addEventListener('portfolio-theme-change',resize);new ResizeObserver(resize).observe(box);image.onload=()=>{ready=true;resize();};image.src='/assets/maos-constelacao.png';resize();
})();
