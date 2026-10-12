// Extra shapes for the game's roster and world (same part/pen API as chars.js).
Object.assign(C,{
mushroom(pen,cx,cy,s){const T=mk(cx,cy,s);
  pen.part('stem',c=>{c.moveTo(...T(-7,-2));c.lineTo(...T(7,-2));c.quadraticCurveTo(...T(9,12),...T(6,14));c.lineTo(...T(-6,14));c.quadraticCurveTo(...T(-9,12),...T(-7,-2));});
  pen.part('cap',c=>{c.moveTo(...T(-16,0));c.quadraticCurveTo(...T(-16,-18),...T(0,-18));c.quadraticCurveTo(...T(16,-18),...T(16,0));c.quadraticCurveTo(...T(0,4),...T(-16,0));});
  pen.part('spot',c=>{c.arc(...T(-7,-8),3*s,0,7);});pen.part('spot',c=>{c.arc(...T(5,-11),2.5*s,0,7);});pen.part('spot',c=>{c.arc(...T(9,-4),2*s,0,7);});
  pen.dot('eye',...T(-3,5),1.6*s);pen.dot('eye',...T(3,5),1.6*s);
},
pixie(pen,cx,cy,s){const T=mk(cx,cy,s);
  pen.orb('glow',...T(0,0),9*s);
  for(const [x,y,rx,ry,a] of [[-9,-6,8,5,-0.5],[9,-6,8,5,0.5],[-8,5,6,4,0.4],[8,5,6,4,-0.4]])pen.part('wing',c=>{c.ellipse(...T(x,y),rx*s,ry*s,a,0,7);});
  pen.part('body',c=>{c.moveTo(...T(-4,0));c.quadraticCurveTo(...T(0,14),...T(4,0));c.closePath();});
  pen.part('body',c=>{c.arc(...T(0,-4),5*s,0,7);});
  pen.part('hair',c=>{c.moveTo(...T(-5,-5));c.quadraticCurveTo(...T(0,-13),...T(5,-5));c.quadraticCurveTo(...T(0,-8),...T(-5,-5));});
  pen.dot('eye',...T(-2,-3),1.2*s);pen.dot('eye',...T(2,-3),1.2*s);
},
mole(pen,cx,cy,s){const T=mk(cx,cy,s);
  pen.part('dirt',c=>{c.ellipse(...T(0,10),16*s,6*s,0,0,7);});
  pen.part('body',c=>{c.moveTo(...T(-12,10));c.quadraticCurveTo(...T(-14,-14),...T(0,-14));c.quadraticCurveTo(...T(14,-14),...T(12,10));c.closePath();});
  for(const sd of [-1,1]){pen.part('claw',c=>{c.moveTo(...T(sd*8,2));c.lineTo(...T(sd*17,0));c.lineTo(...T(sd*16,4));c.lineTo(...T(sd*18,6));c.lineTo(...T(sd*15,9));c.lineTo(...T(sd*9,8));c.closePath();});}
  pen.part('nose',c=>{c.arc(...T(0,-1),3*s,0,7);});
  pen.dot('eye',...T(-4,-6),1.4*s);pen.dot('eye',...T(4,-6),1.4*s);
},
canopy(pen,cx,cy,s){const T=mk(cx,cy,s);
  pen.part('leafDark',c=>{for(let i=0;i<7;i++){const a=i/7*Math.PI*2;const [x,y]=[Math.cos(a)*9,Math.sin(a)*9];c.moveTo(...T(x+11,y));c.arc(...T(x,y),11*s,0,7);}c.moveTo(...T(12,0));c.arc(...T(0,0),12*s,0,7);});
  pen.part('leaf',c=>{for(const [x,y,r] of [[-4,-4,10],[5,-6,7],[-8,3,6]]){c.moveTo(...T(x+r,y));c.arc(...T(x,y),r*s,0,7);}});
  pen.part('leafLight',c=>{for(const [x,y,r] of [[-6,-7,4],[3,-10,3]]){c.moveTo(...T(x+r,y));c.arc(...T(x,y),r*s,0,7);}});
},
pillar(pen,cx,cy,s){const T=mk(cx,cy,s);
  pen.part('base',c=>{c.rect(...T(-17,-17),34*s,34*s);});
  pen.part('stone',c=>{c.arc(...T(0,0),14*s,0,7);});
  pen.part('stoneDark',c=>{c.arc(...T(0,0),10*s,0,7);});
  pen.part('stoneLight',c=>{c.arc(...T(-3,-3),6*s,0,7);});
},
rock(pen,cx,cy,s){const T=mk(cx,cy,s);
  pen.part('stoneDark',c=>{c.moveTo(...T(-18,4));c.lineTo(...T(-14,-10));c.lineTo(...T(-4,-17));c.lineTo(...T(9,-15));c.lineTo(...T(18,-5));c.lineTo(...T(17,8));c.lineTo(...T(6,17));c.lineTo(...T(-10,16));c.closePath();});
  pen.part('stone',c=>{c.moveTo(...T(-14,1));c.lineTo(...T(-11,-9));c.lineTo(...T(-3,-14));c.lineTo(...T(8,-12));c.lineTo(...T(14,-4));c.lineTo(...T(10,6));c.lineTo(...T(-4,8));c.closePath();});
  pen.part('stoneLight',c=>{c.moveTo(...T(-9,-7));c.lineTo(...T(-3,-11));c.lineTo(...T(4,-10));c.lineTo(...T(-2,-5));c.closePath();});
  pen.part('crystal',c=>{c.moveTo(...T(6,-4));c.lineTo(...T(9,-16));c.lineTo(...T(12,-4));c.closePath();});
},
});
