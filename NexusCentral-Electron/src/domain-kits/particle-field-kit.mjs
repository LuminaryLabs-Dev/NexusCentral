export function installParticleField(canvas) {
  if (!canvas) {
    return () => {};
  }

  const context = canvas.getContext('2d', { alpha: true });
  if (!context) {
    return () => {};
  }

  const pointer = { x: 0, y: 0, active: false };
  const particles = Array.from({ length: 18 }, (_, index) => ({
    x: (index * 97) % 1200,
    y: (index * 53) % 760,
    vx: 0.08 + (index % 5) * 0.018,
    vy: 0.035 + (index % 7) * 0.012,
    size: 1 + (index % 3) * 0.45
  }));

  let width = 0;
  let height = 0;
  let frame = 0;
  let animationId = 0;

  function resize() {
    const ratio = Math.min(window.devicePixelRatio || 1, 2);
    width = canvas.clientWidth;
    height = canvas.clientHeight;
    canvas.width = Math.max(1, Math.floor(width * ratio));
    canvas.height = Math.max(1, Math.floor(height * ratio));
    context.setTransform(ratio, 0, 0, ratio, 0, 0);
  }

  function draw() {
    frame += 1;
    context.clearRect(0, 0, width, height);

    for (const particle of particles) {
      const dx = pointer.x - particle.x;
      const dy = pointer.y - particle.y;
      const distance = Math.hypot(dx, dy);
      if (pointer.active && distance < 150) {
        particle.x -= dx * 0.0025;
        particle.y -= dy * 0.0025;
      }

      particle.x += particle.vx + Math.sin(frame * 0.006 + particle.y) * 0.05;
      particle.y += particle.vy + Math.cos(frame * 0.005 + particle.x) * 0.04;

      if (particle.x > width + 10) particle.x = -10;
      if (particle.y > height + 10) particle.y = -10;

      context.beginPath();
      context.fillStyle = 'rgba(94, 208, 165, 0.08)';
      context.arc(particle.x, particle.y, particle.size, 0, Math.PI * 2);
      context.fill();
    }

    for (let index = 0; index < particles.length; index += 1) {
      const particle = particles[index];
      const next = particles[(index + 9) % particles.length];
      const distance = Math.hypot(particle.x - next.x, particle.y - next.y);
      if (distance < 120) {
        context.beginPath();
        context.strokeStyle = `rgba(133, 240, 200, ${0.04 - distance / 3500})`;
        context.moveTo(particle.x, particle.y);
        context.lineTo(next.x, next.y);
        context.stroke();
      }
    }

    animationId = window.requestAnimationFrame(draw);
  }

  function updatePointer(event) {
    pointer.x = event.clientX;
    pointer.y = event.clientY;
    pointer.active = true;
  }

  function clearPointer() {
    pointer.active = false;
  }

  resize();
  draw();
  window.addEventListener('resize', resize);
  window.addEventListener('pointermove', updatePointer);
  window.addEventListener('pointerleave', clearPointer);

  return () => {
    window.cancelAnimationFrame(animationId);
    window.removeEventListener('resize', resize);
    window.removeEventListener('pointermove', updatePointer);
    window.removeEventListener('pointerleave', clearPointer);
  };
}
