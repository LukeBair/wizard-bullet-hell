// Characters are built from named parts; each style's pen decides how a part is rendered.
// pen.part(role, buildPath) ; pen.dot(role,x,y,r) ; pen.shadow(x,y,rx,ry)
function mk(cx,cy,s){return (x,y)=>[cx+x*s,cy+y*s];}
const C={
wizard(pen,cx,cy,s,o={}){const T=mk(cx,cy,s);
  pen.shadow(...T(0,19),17*s,5*s);
  // staff
  pen.part('staff',c=>{const a=T(19,20),b=T(21,-20),w=1.8*s;c.moveTo(a[0]-w,a[1]);c.lineTo(b[0]-w,b[1]);c.lineTo(b[0]+w,b[1]);c.lineTo(a[0]+w,a[1]);c.closePath();});
  // robe
  pen.part('robe',c=>{c.moveTo(...T(-10,-4));c.lineTo(...T(10,-4));c.quadraticCurveTo(...T(15,8),...T(16,17));c.quadraticCurveTo(...T(0,22),...T(-16,17));c.quadraticCurveTo(...T(-15,8),...T(-10,-4));});
  pen.part('robe2',c=>{c.moveTo(...T(-3,-2));c.lineTo(...T(3,-2));c.lineTo(...T(4,20));c.lineTo(...T(-4,20));c.closePath();});
  // sleeves + hands
  pen.part('robe',c=>{c.ellipse(...T(-13,6),5*s,7*s,0.4,0,7);});
  pen.part('robe',c=>{c.ellipse(...T(14,6),5*s,7*s,-0.4,0,7);});
  pen.part('skin',c=>{c.arc(...T(18,10),3.2*s,0,7);});
  pen.part('skin',c=>{c.arc(...T(-15,11),3*s,0,7);});
  // head + beard
  pen.part('skin',c=>{c.arc(...T(0,-9),8.5*s,0,7);});
  pen.part('beard',c=>{c.moveTo(...T(-8,-8));c.quadraticCurveTo(...T(-7,6),...T(0,10));c.quadraticCurveTo(...T(7,6),...T(8,-8));c.quadraticCurveTo(...T(0,-3),...T(-8,-8));});
  pen.dot('eye',...T(-3.2,-9),1.4*s);pen.dot('eye',...T(3.2,-9),1.4*s);
  // hat
  pen.part('hat',c=>{c.ellipse(...T(0,-15),16*s,5*s,0,0,7);});
  pen.part('hat',c=>{c.moveTo(...T(-10,-16));c.quadraticCurveTo(...T(-6,-30),...T(5,-42));c.quadraticCurveTo(...T(4,-30),...T(10,-16));c.quadraticCurveTo(...T(0,-13),...T(-10,-16));});
  pen.part('band',c=>{c.moveTo(...T(-10,-17));c.quadraticCurveTo(...T(0,-14),...T(10,-17));c.lineTo(...T(9.5,-20.5));c.quadraticCurveTo(...T(0,-18),...T(-9.5,-20.5));c.closePath();});
  pen.star('accent',...T(-2,-27),3.2*s);
  // orb
  pen.orb('orb',...T(21,-23),5*s);
},
goblin(pen,cx,cy,s){const T=mk(cx,cy,s);
  pen.shadow(...T(0,13),12*s,4*s);
  pen.part('wood',c=>{c.moveTo(...T(10,10));c.lineTo(...T(13,-6));c.lineTo(...T(19,-8));c.lineTo(...T(16,-12));c.lineTo(...T(10,-8));c.lineTo(...T(7,9));c.closePath();});
  pen.part('cloth',c=>{c.ellipse(...T(0,5),10*s,9*s,0,0,7);});
  pen.part('green',c=>{c.moveTo(...T(-7,-7));c.lineTo(...T(-19,-12));c.lineTo(...T(-8,-2));c.closePath();});
  pen.part('green',c=>{c.moveTo(...T(7,-7));c.lineTo(...T(19,-12));c.lineTo(...T(8,-2));c.closePath();});
  pen.part('green',c=>{c.arc(...T(0,-5),9*s,0,7);});
  pen.dot('eyeE',...T(-3.5,-6),2*s);pen.dot('eyeE',...T(3.5,-6),2*s);
},
slime(pen,cx,cy,s,role='slime'){const T=mk(cx,cy,s);
  pen.shadow(...T(0,8),12*s,3.5*s);
  pen.part(role,c=>{c.moveTo(...T(-12,7));c.quadraticCurveTo(...T(-13,-10),...T(0,-11));c.quadraticCurveTo(...T(13,-10),...T(12,7));c.quadraticCurveTo(...T(0,10),...T(-12,7));});
  pen.part('shine',c=>{c.ellipse(...T(-5,-5),3*s,2*s,-0.6,0,7);});
  pen.dot('eye',...T(-3,0),1.6*s);pen.dot('eye',...T(4,0),1.6*s);
},
bat(pen,cx,cy,s){const T=mk(cx,cy,s);
  pen.shadow(...T(0,16),10*s,3*s);
  pen.part('wing',c=>{c.moveTo(...T(-4,-2));c.lineTo(...T(-22,-9));c.lineTo(...T(-18,1));c.lineTo(...T(-13,-1));c.lineTo(...T(-10,5));c.lineTo(...T(-4,3));c.closePath();});
  pen.part('wing',c=>{c.moveTo(...T(4,-2));c.lineTo(...T(22,-9));c.lineTo(...T(18,1));c.lineTo(...T(13,-1));c.lineTo(...T(10,5));c.lineTo(...T(4,3));c.closePath();});
  pen.part('batbody',c=>{c.moveTo(...T(-5,-5));c.lineTo(...T(-6,-11));c.lineTo(...T(-2,-7));c.lineTo(...T(2,-7));c.lineTo(...T(6,-11));c.lineTo(...T(5,-5));c.quadraticCurveTo(...T(7,6),...T(0,7));c.quadraticCurveTo(...T(-7,6),...T(-5,-5));});
  pen.dot('eyeE',...T(-2,-3),1.3*s);pen.dot('eyeE',...T(2,-3),1.3*s);
},
book(pen,cx,cy,s){const T=mk(cx,cy,s);
  pen.shadow(...T(0,16),11*s,3*s);
  pen.part('cover',c=>{c.moveTo(...T(0,-6));c.lineTo(...T(-15,-10));c.lineTo(...T(-16,6));c.lineTo(...T(0,9));c.lineTo(...T(16,6));c.lineTo(...T(15,-10));c.closePath();});
  pen.part('page',c=>{c.moveTo(...T(0,-4));c.lineTo(...T(-12,-7));c.lineTo(...T(-13,4));c.lineTo(...T(0,7));c.lineTo(...T(13,4));c.lineTo(...T(12,-7));c.closePath();});
  pen.dot('eyeE',...T(-6,-1),1.8*s);pen.dot('eyeE',...T(6,-1),1.8*s);
},
knight(pen,cx,cy,s){const T=mk(cx,cy,s);
  pen.shadow(...T(0,18),15*s,4.5*s);
  pen.part('blade',c=>{c.moveTo(...T(14,6));c.lineTo(...T(16,-26));c.lineTo(...T(19,-30));c.lineTo(...T(20,-25));c.lineTo(...T(18,6));c.closePath();});
  pen.part('wood',c=>{c.rect(...T(11,5),9*s,3*s);});
  pen.part('metal',c=>{c.moveTo(...T(-11,-3));c.lineTo(...T(11,-3));c.lineTo(...T(13,16));c.lineTo(...T(-13,16));c.closePath();});
  pen.part('metal2',c=>{c.ellipse(...T(-13,0),5*s,4*s,0,0,7);});
  pen.part('metal2',c=>{c.ellipse(...T(13,0),5*s,4*s,0,0,7);});
  pen.part('metal',c=>{c.moveTo(...T(-8,-4));c.lineTo(...T(-8,-15));c.quadraticCurveTo(...T(0,-22),...T(8,-15));c.lineTo(...T(8,-4));c.closePath();});
  pen.part('visor',c=>{c.rect(...T(-6,-12),12*s,2.6*s);});
  pen.part('plume',c=>{c.moveTo(...T(-1,-19));c.quadraticCurveTo(...T(-6,-30),...T(4,-32));c.quadraticCurveTo(...T(2,-25),...T(2,-19));c.closePath();});
  pen.part('shield',c=>{c.moveTo(...T(-22,-6));c.lineTo(...T(-10,-6));c.lineTo(...T(-10,6));c.quadraticCurveTo(...T(-16,14),...T(-16,14));c.quadraticCurveTo(...T(-22,8),...T(-22,6));c.closePath();});
},
wolf(pen,cx,cy,s){const T=mk(cx,cy,s);
  pen.shadow(...T(0,4),12*s,22*s);
  pen.part('fur2',c=>{c.moveTo(...T(-3,18));c.quadraticCurveTo(...T(-8,30),...T(0,36));c.quadraticCurveTo(...T(4,28),...T(3,18));c.closePath();});
  pen.part('fur',c=>{c.ellipse(...T(0,6),10*s,16*s,0,0,7);});
  pen.part('fur2',c=>{c.ellipse(...T(-9,-2),3.5*s,5*s,0.3,0,7);});pen.part('fur2',c=>{c.ellipse(...T(9,-2),3.5*s,5*s,-0.3,0,7);});
  pen.part('fur2',c=>{c.ellipse(...T(-8,14),3.5*s,5*s,-0.3,0,7);});pen.part('fur2',c=>{c.ellipse(...T(8,14),3.5*s,5*s,0.3,0,7);});
  pen.part('fur',c=>{c.moveTo(...T(-8,-8));c.lineTo(...T(-9,-20));c.lineTo(...T(-4,-14));c.lineTo(...T(4,-14));c.lineTo(...T(9,-20));c.lineTo(...T(8,-8));c.quadraticCurveTo(...T(4,-2),...T(0,4));c.quadraticCurveTo(...T(-4,-2),...T(-8,-8));});
  pen.part('snout',c=>{c.ellipse(...T(0,-1),4*s,5*s,0,0,7);});
  pen.dot('eyeE',...T(-4,-9),1.6*s);pen.dot('eyeE',...T(4,-9),1.6*s);
},
golem(pen,cx,cy,s,role='rock'){const T=mk(cx,cy,s);
  pen.shadow(...T(0,30),40*s,10*s);
  pen.part(role,c=>{c.moveTo(...T(-38,-6));c.lineTo(...T(-50,8));c.lineTo(...T(-44,26));c.lineTo(...T(-30,22));c.lineTo(...T(-26,4));c.closePath();});
  pen.part(role,c=>{c.moveTo(...T(38,-6));c.lineTo(...T(50,8));c.lineTo(...T(44,26));c.lineTo(...T(30,22));c.lineTo(...T(26,4));c.closePath();});
  pen.part(role,c=>{c.moveTo(...T(-30,-16));c.lineTo(...T(-12,-28));c.lineTo(...T(14,-28));c.lineTo(...T(32,-14));c.lineTo(...T(28,18));c.lineTo(...T(10,28));c.lineTo(...T(-12,28));c.lineTo(...T(-28,16));c.closePath();});
  pen.part(role+'2',c=>{c.moveTo(...T(-14,-20));c.lineTo(...T(14,-20));c.lineTo(...T(18,-6));c.lineTo(...T(0,0));c.lineTo(...T(-18,-6));c.closePath();});
  pen.part('crystal',c=>{c.moveTo(...T(-6,-30));c.lineTo(...T(-2,-46));c.lineTo(...T(3,-30));c.closePath();});
  pen.part('crystal',c=>{c.moveTo(...T(4,-29));c.lineTo(...T(12,-42));c.lineTo(...T(13,-27));c.closePath();});
  pen.part('crystal',c=>{c.moveTo(...T(-16,-26));c.lineTo(...T(-18,-38));c.lineTo(...T(-9,-28));c.closePath();});
  pen.part('crystal',c=>{c.moveTo(...T(-6,6));c.lineTo(...T(0,0));c.lineTo(...T(6,6));c.lineTo(...T(0,14));c.closePath();});
  pen.dot('eyeE',...T(-8,-11),3*s);pen.dot('eyeE',...T(8,-11),3*s);
},
treant(pen,cx,cy,s){const T=mk(cx,cy,s);
  pen.shadow(...T(0,34),46*s,11*s);
  for(const [a,l] of [[-2.6,52],[-0.5,52],[2.2,46],[0.9,44],[-1.5,48]]){pen.part('bark2',c=>{const w=6;const ex=Math.cos(a)*l,ey=Math.sin(a)*l*0.8+10;c.moveTo(...T(-w,10));c.quadraticCurveTo(...T(ex*0.5-w,ey*0.5+6),...T(ex,ey));c.quadraticCurveTo(...T(ex*0.5+w,ey*0.5+12),...T(w,14));c.closePath();});}
  pen.part('leaf',c=>{for(const [x,y,r] of [[-26,-14,20],[24,-12,20],[0,-30,22],[-14,-34,15],[16,-32,16]])c.moveTo(...T(x+r,y)),c.arc(...T(x,y),r*s,0,7);});
  pen.part('bark',c=>{c.moveTo(...T(-20,-14));c.quadraticCurveTo(...T(0,-24),...T(20,-14));c.lineTo(...T(22,22));c.quadraticCurveTo(...T(0,30),...T(-22,22));c.closePath();});
  pen.part('hole',c=>{c.ellipse(...T(0,10),8*s,5*s,0,0,7);});
  pen.dot('eyeE',...T(-8,-4),3*s);pen.dot('eyeE',...T(8,-4),3*s);
},
skeleton(pen,cx,cy,s){const T=mk(cx,cy,s);
  pen.shadow(...T(0,17),12*s,4*s);
  pen.part('blade',c=>{c.moveTo(...T(12,8));c.lineTo(...T(13,-22));c.lineTo(...T(16,-24));c.lineTo(...T(16,8));c.closePath();});
  pen.part('bone',c=>{c.moveTo(...T(-8,-2));c.lineTo(...T(8,-2));c.lineTo(...T(6,15));c.lineTo(...T(-6,15));c.closePath();});
  pen.part('bone2',c=>{for(let i=0;i<3;i++){c.rect(...T(-6,1+i*4),12*s,1.6*s);}});
  pen.part('bone',c=>{c.arc(...T(0,-9),8*s,0,7);});
  pen.dot('eyeE',...T(-3,-9),2.2*s);pen.dot('eyeE',...T(3,-9),2.2*s);
},
};
