import React from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';

const Layout: React.FC = () => {
  const location = useLocation();

  const isActive = (path: string): boolean => {
    return location.pathname === path;
  };

  const navLinkClass = (path: string): string => {
    const baseClass = 'px-4 py-2 rounded-md text-sm font-medium transition-colors';
    return isActive(path)
      ? `${baseClass} bg-blue-600 text-white`
      : `${baseClass} text-gray-700 hover:bg-gray-100`;
  };

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="bg-white shadow-sm">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between items-center py-4">
            <div className="flex items-center">
              <h1 className="text-2xl font-bold text-gray-900">
                SWIFT Message Processor
              </h1>
            </div>
            <nav className="flex space-x-4">
              <Link to="/" className={navLinkClass('/')}>
                Dashboard
              </Link>
              <Link to="/messages" className={navLinkClass('/messages')}>
                Messages
              </Link>
              <Link to="/settings" className={navLinkClass('/settings')}>
                Settings
              </Link>
            </nav>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-7xl mx-auto py-6 sm:px-6 lg:px-8">
        <Outlet />
      </main>
    </div>
  );
};

export default Layout;
