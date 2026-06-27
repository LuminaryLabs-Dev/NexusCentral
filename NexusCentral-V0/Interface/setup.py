from setuptools import setup, find_packages

setup(
    name="nexuscentral_interface",
    version="0.1.0",
    packages=find_packages(where="src"),
    package_dir={"": "src"},
    install_requires=["huggingface_hub"],
    entry_points={
        "console_scripts": [
            "nci = nexuscentral_interface.cli:main"
        ]
    }
)
