import React, { useState } from 'react';

interface TestModeConfigProps {
  enabled: boolean;
  onToggle: (enabled: boolean) => void;
  updating: boolean;
}

const TestModeConfig: React.FC<TestModeConfigProps> = ({ enabled, onToggle, updating }) => {
  const [showConfirmation, setShowConfirmation] = useState<boolean>(false);

  const handleToggle = (): void => {
    if (enabled) {
      // Show confirmation when disabling
      setShowConfirmation(true);
    } else {
      // Enable directly
      onToggle(true);
    }
  };

  const confirmDisable = (): void => {
    onToggle(false);
    setShowConfirmation(false);
  };

  const cancelDisable = (): void => {
    setShowConfirmation(false);
  };

  return (
    <div className="bg-white rounded-lg shadow p-6">
      <h2 className="text-xl font-semibold text-gray-900 mb-4">Test Mode Configuration</h2>

      <div className="space-y-4">
        {/* Test Mode Toggle */}
        <div className="flex items-center justify-between bg-gray-50 rounded-lg p-4">
          <div className="flex-1">
            <h3 className="text-sm font-medium text-gray-900">Test Message Generation</h3>
            <p className="text-sm text-gray-600 mt-1">
              Automatically generate test MT103 messages for development and testing
            </p>
          </div>
          <button
            onClick={handleToggle}
            disabled={updating}
            className={`relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 ${
              enabled ? 'bg-blue-600' : 'bg-gray-200'
            } ${updating ? 'opacity-50 cursor-not-allowed' : ''}`}
          >
            <span
              className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${
                enabled ? 'translate-x-5' : 'translate-x-0'
              }`}
            />
          </button>
        </div>

        {/* Status Information */}
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
          <div className="flex">
            <svg
              className="w-5 h-5 text-blue-600 mr-2 flex-shrink-0"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
              />
            </svg>
            <div className="flex-1">
              <p className="text-sm text-blue-700">
                {enabled
                  ? 'Test mode is currently enabled. The system will generate test messages every 10 seconds.'
                  : 'Test mode is currently disabled. Enable it to start generating test messages.'}
              </p>
            </div>
          </div>
        </div>

        {/* Test Mode Details */}
        {enabled && (
          <div className="border border-gray-200 rounded-lg p-4">
            <h4 className="text-sm font-medium text-gray-900 mb-3">Test Mode Details</h4>
            <dl className="space-y-2">
              <div className="flex justify-between text-sm">
                <dt className="text-gray-600">Generation Interval</dt>
                <dd className="text-gray-900 font-medium">10 seconds</dd>
              </div>
              <div className="flex justify-between text-sm">
                <dt className="text-gray-600">Message Types</dt>
                <dd className="text-gray-900 font-medium">MT103</dd>
              </div>
              <div className="flex justify-between text-sm">
                <dt className="text-gray-600">Valid/Invalid Ratio</dt>
                <dd className="text-gray-900 font-medium">80% / 20%</dd>
              </div>
            </dl>
          </div>
        )}
      </div>

      {/* Confirmation Modal */}
      {showConfirmation && (
        <div className="fixed inset-0 z-50 overflow-y-auto">
          <div className="flex items-center justify-center min-h-screen px-4 pt-4 pb-20 text-center sm:block sm:p-0">
            <div
              className="fixed inset-0 transition-opacity bg-gray-500 bg-opacity-75"
              onClick={cancelDisable}
            ></div>

            <div className="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-lg sm:w-full">
              <div className="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
                <div className="sm:flex sm:items-start">
                  <div className="mx-auto flex-shrink-0 flex items-center justify-center h-12 w-12 rounded-full bg-yellow-100 sm:mx-0 sm:h-10 sm:w-10">
                    <svg
                      className="h-6 w-6 text-yellow-600"
                      fill="none"
                      stroke="currentColor"
                      viewBox="0 0 24 24"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                      />
                    </svg>
                  </div>
                  <div className="mt-3 text-center sm:mt-0 sm:ml-4 sm:text-left">
                    <h3 className="text-lg leading-6 font-medium text-gray-900">
                      Disable Test Mode
                    </h3>
                    <div className="mt-2">
                      <p className="text-sm text-gray-500">
                        Are you sure you want to disable test mode? This will stop automatic test
                        message generation.
                      </p>
                    </div>
                  </div>
                </div>
              </div>
              <div className="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
                <button
                  onClick={confirmDisable}
                  className="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-red-600 text-base font-medium text-white hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-red-500 sm:ml-3 sm:w-auto sm:text-sm"
                >
                  Disable
                </button>
                <button
                  onClick={cancelDisable}
                  className="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 sm:mt-0 sm:ml-3 sm:w-auto sm:text-sm"
                >
                  Cancel
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default TestModeConfig;
