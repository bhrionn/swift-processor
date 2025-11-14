import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import StatCard from '../StatCard';

describe('StatCard', () => {
  const mockIcon = (
    <svg data-testid="test-icon">
      <path d="M0 0" />
    </svg>
  );

  it('renders stat card with title and value', () => {
    render(<StatCard title="Test Stat" value={100} color="blue" icon={mockIcon} />);
    
    expect(screen.getByText('Test Stat')).toBeInTheDocument();
    expect(screen.getByText('100')).toBeInTheDocument();
  });

  it('renders icon', () => {
    render(<StatCard title="Test Stat" value={100} color="blue" icon={mockIcon} />);
    
    expect(screen.getByTestId('test-icon')).toBeInTheDocument();
  });

  it('applies correct color classes for green', () => {
    const { container } = render(
      <StatCard title="Test Stat" value={100} color="green" icon={mockIcon} />
    );
    
    const coloredElement = container.querySelector('[class*="green"]');
    expect(coloredElement).toBeInTheDocument();
  });

  it('applies correct color classes for red', () => {
    const { container } = render(
      <StatCard title="Test Stat" value={100} color="red" icon={mockIcon} />
    );
    
    const coloredElement = container.querySelector('[class*="red"]');
    expect(coloredElement).toBeInTheDocument();
  });

  it('applies correct color classes for yellow', () => {
    const { container } = render(
      <StatCard title="Test Stat" value={100} color="yellow" icon={mockIcon} />
    );
    
    const coloredElement = container.querySelector('[class*="yellow"]');
    expect(coloredElement).toBeInTheDocument();
  });

  it('applies correct color classes for blue', () => {
    const { container } = render(
      <StatCard title="Test Stat" value={100} color="blue" icon={mockIcon} />
    );
    
    const coloredElement = container.querySelector('[class*="blue"]');
    expect(coloredElement).toBeInTheDocument();
  });

  it('handles string values', () => {
    render(<StatCard title="Status" value="Running" color="green" icon={mockIcon} />);
    
    expect(screen.getByText('Running')).toBeInTheDocument();
  });

  it('handles numeric values', () => {
    render(<StatCard title="Count" value={12345} color="blue" icon={mockIcon} />);
    
    expect(screen.getByText('12345')).toBeInTheDocument();
  });

  it('handles zero value', () => {
    render(<StatCard title="Count" value={0} color="blue" icon={mockIcon} />);
    
    expect(screen.getByText('0')).toBeInTheDocument();
  });

  it('has proper card styling', () => {
    const { container } = render(
      <StatCard title="Test Stat" value={100} color="blue" icon={mockIcon} />
    );
    
    const card = container.querySelector('.bg-white');
    expect(card).toBeInTheDocument();
  });
});
